using Carried.Idempotency.AspNet.Metadata;
using Carried.Idempotency.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Carried.Idempotency.AspNet;

public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IdempotencyService idempotencyService, RequestFingerprintProvider fingerprintProvider,
        IdempotentResponseExecutor responseExecutor)
    {
        Endpoint? endpoint = context.GetEndpoint();

        var metadata =
            endpoint?.Metadata.GetMetadata<IdempotencyMetadata>();

        if (metadata is null)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(
                "Idempotency-Key",
                out StringValues values) ||
            values.Count != 1 ||
            string.IsNullOrWhiteSpace(values[0]))
        {
            context.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            await context.Response.WriteAsync(
                "Missing or invalid Idempotency-Key header.");

            return;
        }

        if (endpoint is not RouteEndpoint routeEndpoint)
        {
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            await context.Response.WriteAsync(
                "Idempotency requires a route endpoint.");

            return;
        }

        string? routePattern =
            routeEndpoint.RoutePattern.RawText;

        if (string.IsNullOrWhiteSpace(routePattern))
        {
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            await context.Response.WriteAsync(
                "Idempotency requires a route pattern.");

            return;
        }

        string idempotencyKey = values[0]!;

        var scope =
            $"{context.Request.Method}:{routePattern}";

        var key = new IdempotencyKey(
            scope,
            idempotencyKey);

        string fingerprint =
            await fingerprintProvider.CreateAsync(
                context,
                routePattern);

        try
        {
            await responseExecutor.ExecuteAsync(
                context,
                idempotencyService,
                key,
                fingerprint,
                _next);
        }
        catch (Exception exception) when(exception is IdempotencyConflictException or IdempotencyInProgressException or IdempotencyLeaseLostException)
        {
            context.Response.StatusCode = IdempotencyExceptionMapper.GetStatusCode(exception);

            await context.Response.WriteAsJsonAsync(new
            {
                error = IdempotencyExceptionMapper.GetErrorCode(exception),
                errorDescription = exception.Message,
            });
        }
    }
}