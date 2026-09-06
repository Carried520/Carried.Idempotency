namespace Carried.Idempotency.AspNet.Errors;

/// <summary>
/// Describes an HTTP error produced by the ASP.NET Core idempotency integration.
/// </summary>
/// <param name="StatusCode">The HTTP status code to return.</param>
/// <param name="Code">A stable machine-readable error code.</param>
/// <param name="Description">A human-readable description of the error.</param>
public sealed record IdempotencyError(int StatusCode, string Code, string Description);