using Microsoft.AspNetCore.Http;

namespace Carried.Idempotency.AspNet.Errors;

public interface IIdempotencyErrorResponseWriter
{
    Task WriteAsync(HttpContext context, IdempotencyError error, CancellationToken cancellationToken = default);
}