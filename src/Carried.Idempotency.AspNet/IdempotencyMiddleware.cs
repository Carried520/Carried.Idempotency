using Carried.Idempotency.AspNet.Errors;
using Carried.Idempotency.AspNet.Fingerprinting;
using Carried.Idempotency.AspNet.Metadata;
using Carried.Idempotency.AspNet.Options;
using Carried.Idempotency.AspNet.Policies;
using Carried.Idempotency.AspNet.Responses;
using Carried.Idempotency.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Carried.Idempotency.AspNet;

internal sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IdempotencyAspNetOptions _options;

    public IdempotencyMiddleware(RequestDelegate next, IOptions<IdempotencyAspNetOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, IdempotencyService idempotencyService, IdempotentResponseExecutor responseExecutor,
        IEnumerable<IIdempotencyFingerprintContributor> contributors)
    {
        Endpoint? endpoint = context.GetEndpoint();

        var metadata =
            endpoint?.Metadata.GetMetadata<IdempotencyMetadata>();

        if (metadata is null)
        {
            await _next(context);
            return;
        }

        IdempotencyPolicy policy =
            metadata.PolicyName is null
                ? _options.DefaultPolicy
                : ResolvePolicy(metadata.PolicyName);

        if (!context.Request.Headers.TryGetValue(
                _options.HeaderName,
                out StringValues values) ||
            values.Count != 1 ||
            string.IsNullOrWhiteSpace(values[0]) || values[0]!.Length > policy.MaxKeyLength)
        {
            context.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            await context.Response.WriteAsync(
                $"Missing or invalid {_options.HeaderName} header.");

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
            await RequestFingerprintProvider.CreateAsync(
                context,
                routePattern,
                contributors,
                context.RequestAborted);

        try
        {
            await responseExecutor.ExecuteAsync(
                context,
                idempotencyService,
                key,
                fingerprint,
                policy,
                _next);
        }
        catch (Exception exception) when (exception is IdempotencyConflictException or IdempotencyInProgressException or IdempotencyLeaseLostException)
        {
            context.Response.StatusCode = IdempotencyExceptionMapper.GetStatusCode(exception);

            await context.Response.WriteAsJsonAsync(new
            {
                error = IdempotencyExceptionMapper.GetErrorCode(exception),
                errorDescription = exception.Message,
            });
        }
    }

    private IdempotencyPolicy ResolvePolicy(string policyName)
    {
        if (_options.Policies.TryGetValue(policyName, out IdempotencyPolicy? policy))
        {
            return policy;
        }

        throw new InvalidOperationException($"Idempotency policy '{policyName}' is not configured.");
    }
}