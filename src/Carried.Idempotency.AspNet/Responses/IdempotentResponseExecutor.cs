using Carried.Idempotency.AspNet.Observability;
using Carried.Idempotency.AspNet.Policies;
using Carried.Idempotency.IdempotencyOperation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Carried.Idempotency.AspNet.Responses;

internal sealed class IdempotentResponseExecutor
{
    private readonly IIdempotencyMetricsRecorder? _metrics;

    public IdempotentResponseExecutor(IIdempotencyMetricsRecorder? metrics = null)
    {
        _metrics = metrics;
    }

    internal async Task ExecuteAsync(
        HttpContext context,
        IdempotencyService idempotencyService,
        IdempotencyKey key,
        string fingerprint,
        IdempotencyPolicy policy,
        RequestDelegate next)
    {
        Stream originalBody = context.Response.Body;

        await using var responseBuffer =
            new MemoryStream();

        context.Response.Body = responseBuffer;

        IdempotentHttpResponse? response;

        try
        {
            response = await idempotencyService.ExecuteAsync(
                key,
                fingerprint,
                async cancellationToken =>
                {
                    CancellationToken originalRequestAborted =
                        context.RequestAborted;

                    context.RequestAborted =
                        cancellationToken;

                    try
                    {
                        await next(context);
                    }
                    finally
                    {
                        context.RequestAborted =
                            originalRequestAborted;
                    }

                    byte[] body =
                        responseBuffer.ToArray();

                    var headers = new Dictionary<string, string[]>(
                        StringComparer.OrdinalIgnoreCase);

                    foreach (KeyValuePair<string, StringValues> header
                             in context.Response.Headers)
                    {
                        if (!policy.ReplayHeaders.Contains(header.Key))
                            continue;

                        headers[header.Key] =
                            header.Value.OfType<string>().ToArray();
                    }

                    var idempotentHttpResponse =
                        new IdempotentHttpResponse(
                            context.Response.StatusCode,
                            context.Response.ContentType,
                            headers,
                            body);

                    ResponseRetentionResult retentionResult =
                        GetRetentionResult(
                            idempotentHttpResponse,
                            policy);

                    _metrics?.RecordResponse(
                        retentionResult.MetricResult);

                    return retentionResult.ShouldStore
                        ? IdempotencyOperationResult<IdempotentHttpResponse>
                            .Complete(idempotentHttpResponse)
                        : IdempotencyOperationResult<IdempotentHttpResponse>
                            .Release(idempotentHttpResponse);
                },
                context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        if (response is null)
        {
            throw new InvalidOperationException(
                "The idempotency operation returned no HTTP response.");
        }

        context.Response.StatusCode =
            response.StatusCode;

        context.Response.ContentType =
            response.ContentType;

        foreach (KeyValuePair<string, string[]> header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value;
        }

        await context.Response.Body.WriteAsync(
            response.Body,
            context.RequestAborted);
    }

    private static ResponseRetentionResult GetRetentionResult(
        IdempotentHttpResponse response,
        IdempotencyPolicy policy)
    {
        string? rejectionReason =
            response.StatusCode switch
            {
                >= 200 and < 400 => null,

                408 =>
                    IdempotencyMetricResults.RequestTimeout,

                429 =>
                    IdempotencyMetricResults.RateLimited,

                >= 400 and < 500 when !policy.StoreClientErrors =>
                    IdempotencyMetricResults.ClientErrorPolicy,

                >= 400 and < 500 => null,

                >= 500 and < 600 =>
                    IdempotencyMetricResults.ServerError,

                _ => throw new ArgumentOutOfRangeException(
                    nameof(response))
            };

        if (rejectionReason is not null)
        {
            return new ResponseRetentionResult(
                false,
                rejectionReason);
        }

        if (response.Body.LongLength >
            policy.MaxRetainedResponseBodySize)
        {
            return new ResponseRetentionResult(
                false,
                IdempotencyMetricResults.ResponseTooLarge);
        }

        return new ResponseRetentionResult(
            true,
            IdempotencyMetricResults.Retained);
    }

    private readonly record struct ResponseRetentionResult(
        bool ShouldStore,
        string MetricResult);
}