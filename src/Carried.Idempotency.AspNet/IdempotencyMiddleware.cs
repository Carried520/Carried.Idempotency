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

/// <summary>
/// Coordinates idempotency handling for endpoints that opt in through
/// <see cref="IdempotencyMetadata"/>.
/// </summary>
/// <remarks>
/// <para>
/// The middleware is intentionally limited to HTTP orchestration. Core idempotency
/// ownership, lease handling, completion, release, replay state, and conflict
/// detection are delegated to <see cref="IdempotencyService"/>.
/// </para>
/// <para>
/// For idempotent endpoints, the middleware resolves the applicable HTTP policy,
/// validates the configured idempotency-key header, derives a stable operation
/// scope from the HTTP method and route pattern, creates a request fingerprint,
/// and delegates execution and response capture to
/// <see cref="IdempotentResponseExecutor"/>.
/// </para>
/// <para>
/// Endpoints without <see cref="IdempotencyMetadata"/> pass through unchanged.
/// The middleware must therefore execute after routing so that endpoint metadata
/// and route information are available.
/// </para>
/// </remarks>
internal sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IdempotencyAspNetOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyMiddleware"/> class.
    /// </summary>
    /// <param name="next">
    /// The next request delegate in the ASP.NET Core pipeline.
    /// </param>
    /// <param name="options">
    /// The configured ASP.NET Core idempotency options.
    /// </param>
    public IdempotencyMiddleware(RequestDelegate next, IOptions<IdempotencyAspNetOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);

        _next = next;
        _options = options.Value;
    }

    /// <summary>
    /// Processes the current request when its endpoint requires idempotency.
    /// </summary>
    /// <param name="context">
    /// The current HTTP context.
    /// </param>
    /// <param name="idempotencyService">
    /// The core idempotency service responsible for operation ownership,
    /// completion, release, and replay.
    /// </param>
    /// <param name="responseExecutor">
    /// The component responsible for executing, capturing, retaining, releasing,
    /// and replaying the HTTP response.
    /// </param>
    /// <param name="contributors">
    /// Application-defined contributors whose values participate in the request
    /// fingerprint.
    /// </param>
    /// <param name="idempotencyErrorResponseWriter">
    /// The writer used to produce HTTP responses for idempotency errors.
    /// </param>
    /// <returns>
    /// A task that represents completion of idempotency processing for the request.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Requests targeting endpoints without idempotency metadata are passed directly
    /// to the next middleware.
    /// </para>
    /// <para>
    /// Marked endpoints require exactly one non-empty configured idempotency-key
    /// header whose value does not exceed the active policy's maximum key length.
    /// Invalid keys produce an HTTP 400 response without invoking the endpoint.
    /// </para>
    /// <para>
    /// The operation scope is formed from the HTTP method and resolved route
    /// pattern. The raw client idempotency key is therefore isolated per logical
    /// route and HTTP method.
    /// </para>
    /// <para>
    /// Core idempotency exceptions and unsupported replayable-response conditions
    /// are translated into integration-level errors and written through
    /// <see cref="IIdempotencyErrorResponseWriter"/>.
    /// </para>
    /// </remarks>
    public async Task InvokeAsync(HttpContext context,
        IdempotencyService idempotencyService,
        IdempotentResponseExecutor responseExecutor,
        IEnumerable<IIdempotencyFingerprintContributor> contributors,
        IIdempotencyErrorResponseWriter idempotencyErrorResponseWriter)
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
            await idempotencyErrorResponseWriter.WriteAsync(
                context,
                new IdempotencyError(
                    StatusCodes.Status400BadRequest,
                    "invalid_idempotency_key",
                    $"Missing or invalid {_options.HeaderName} header."),
                context.RequestAborted);
            return;
        }

        if (endpoint is not RouteEndpoint routeEndpoint)
        {
            await idempotencyErrorResponseWriter.WriteAsync(
                context,
                new IdempotencyError(
                    StatusCodes.Status500InternalServerError,
                    "idempotency_route_endpoint_required",
                    "Idempotency requires a route endpoint."),
                context.RequestAborted);

            return;
        }

        string? routePattern =
            routeEndpoint.RoutePattern.RawText;

        if (string.IsNullOrWhiteSpace(routePattern))
        {
            await idempotencyErrorResponseWriter.WriteAsync(
                context,
                new IdempotencyError(
                    StatusCodes.Status500InternalServerError,
                    "idempotency_route_pattern_required",
                    "Idempotency requires a route pattern."),
                context.RequestAborted);

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
        catch (Exception exception) when (exception is IdempotencyConflictException or IdempotencyInProgressException
                                              or IdempotencyLeaseLostException
                                              or UnsupportedIdempotentResponseException)
        {
            IdempotencyError idempotencyError = IdempotencyExceptionMapper.Map(exception);
            await idempotencyErrorResponseWriter.WriteAsync(context, idempotencyError, context.RequestAborted);
        }
    }

    /// <summary>
    /// Resolves a configured named idempotency policy.
    /// </summary>
    /// <param name="policyName">
    /// The policy name specified by endpoint metadata.
    /// </param>
    /// <returns>
    /// The configured policy associated with <paramref name="policyName"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the endpoint references a policy that has not been configured.
    /// </exception>
    private IdempotencyPolicy ResolvePolicy(string policyName)
    {
        if (_options.Policies.TryGetValue(policyName, out IdempotencyPolicy? policy))
        {
            return policy;
        }

        throw new InvalidOperationException($"Idempotency policy '{policyName}' is not configured.");
    }
}