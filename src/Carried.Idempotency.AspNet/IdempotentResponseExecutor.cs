using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Carried.Idempotency.AspNet;

public sealed class IdempotentResponseExecutor
{
    public async Task ExecuteAsync(
        HttpContext context,
        IdempotencyService idempotencyService,
        IdempotencyKey key,
        string fingerprint,
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
                    foreach (KeyValuePair<string, StringValues> header in context.Response.Headers)
                    {
                        if (!IdempotencyReplayHeaders.Allowed.Contains(header.Key))
                            continue;

                        headers[header.Key] = header.Value.OfType<string>().ToArray();
                    }

                    return new IdempotentHttpResponse(
                        context.Response.StatusCode,
                        context.Response.ContentType,
                        headers,
                        body);
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
}