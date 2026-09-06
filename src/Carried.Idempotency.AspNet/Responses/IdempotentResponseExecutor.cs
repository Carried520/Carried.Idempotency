using Carried.Idempotency.AspNet.Errors;
using Microsoft.Net.Http.Headers;
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
        var isUnsupportedResponse = false;
        Stream originalBody = context.Response.Body;

        await using var responseBuffer =
            new MemoryStream();

        await using var captureStream = new IdempotentResponseCaptureStream(responseBuffer, () => IsUnsupportedResponse(context.Response.ContentType));

        context.Response.Body = captureStream;

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

                    ResponseRetentionOutcome retentionOutcome =
                        GetRetentionOutcome(
                            idempotentHttpResponse,
                            policy);

                    isUnsupportedResponse = retentionOutcome is ResponseRetentionOutcome.UnsupportedResponse;

                    _metrics?.RecordResponse(GetMetricResult(retentionOutcome));

                    return retentionOutcome is ResponseRetentionOutcome.Retained
                        ? IdempotencyOperationResult<IdempotentHttpResponse>
                            .Complete(idempotentHttpResponse)
                        : IdempotencyOperationResult<IdempotentHttpResponse>
                            .Release(idempotentHttpResponse);
                },
                context.RequestAborted);
        }
        catch (UnsupportedIdempotentResponseException)
        {
            _metrics?.RecordResponse(IdempotencyMetricResults.UnsupportedResponse);
            throw;
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

        if (isUnsupportedResponse)
        {
            throw new UnsupportedIdempotentResponseException();
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

    private static ResponseRetentionOutcome GetRetentionOutcome(
        IdempotentHttpResponse response,
        IdempotencyPolicy policy)
    {
        if (IsUnsupportedResponse(response.ContentType))
            return ResponseRetentionOutcome.UnsupportedResponse;

        ResponseRetentionOutcome retentionOutcome =
            response.StatusCode switch
            {
                >= 200 and < 400 => ResponseRetentionOutcome.Retained,

                408 =>
                    ResponseRetentionOutcome.RequestTimeout,

                429 =>
                    ResponseRetentionOutcome.RateLimited,

                >= 400 and < 500 when !policy.StoreClientErrors =>
                    ResponseRetentionOutcome.ClientErrorPolicy,

                >= 400 and < 500 => ResponseRetentionOutcome.Retained,

                >= 500 and < 600 =>
                    ResponseRetentionOutcome.ServerError,

                _ => throw new ArgumentOutOfRangeException(
                    nameof(response))
            };

        if (retentionOutcome is not ResponseRetentionOutcome.Retained)
        {
            return retentionOutcome;
        }

        if (response.Body.LongLength >
            policy.MaxRetainedResponseBodySize)
        {
            return ResponseRetentionOutcome.ResponseTooLarge;
        }

        return ResponseRetentionOutcome.Retained;
    }

    private static string GetMetricResult(ResponseRetentionOutcome outcome)
    {
        return outcome switch
        {
            ResponseRetentionOutcome.Retained => IdempotencyMetricResults.Retained,
            ResponseRetentionOutcome.ServerError => IdempotencyMetricResults.ServerError,
            ResponseRetentionOutcome.RequestTimeout => IdempotencyMetricResults.RequestTimeout,
            ResponseRetentionOutcome.RateLimited => IdempotencyMetricResults.RateLimited,
            ResponseRetentionOutcome.ClientErrorPolicy => IdempotencyMetricResults.ClientErrorPolicy,
            ResponseRetentionOutcome.ResponseTooLarge => IdempotencyMetricResults.ResponseTooLarge,
            ResponseRetentionOutcome.UnsupportedResponse => IdempotencyMetricResults.UnsupportedResponse,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };
    }

    private static bool IsUnsupportedResponse(
        string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        if (!MediaTypeHeaderValue.TryParse(
                contentType,
                out MediaTypeHeaderValue? mediaType))
        {
            return false;
        }

        return mediaType.MediaType.Equals(
            "text/event-stream",
            StringComparison.OrdinalIgnoreCase);
    }
}