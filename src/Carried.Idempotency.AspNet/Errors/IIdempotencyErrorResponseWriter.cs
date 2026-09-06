using Microsoft.AspNetCore.Http;

namespace Carried.Idempotency.AspNet.Errors;

/// <summary>
/// Writes HTTP responses for errors produced by the idempotency integration.
/// </summary>
/// <remarks>
/// Implementations are responsible for writing the complete error response.
/// </remarks>
public interface IIdempotencyErrorResponseWriter
{
    /// <summary>
    /// Writes the specified idempotency error to the HTTP response.
    /// </summary>
    /// <param name="context">The HTTP context for the current request</param>
    /// <param name="error">The idempotency error to write.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    Task WriteAsync(HttpContext context, IdempotencyError error, CancellationToken cancellationToken = default);
}