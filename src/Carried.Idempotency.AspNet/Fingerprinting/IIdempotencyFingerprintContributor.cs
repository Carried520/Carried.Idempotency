using Microsoft.AspNetCore.Http;

namespace Carried.Idempotency.AspNet.Fingerprinting;

public interface IIdempotencyFingerprintContributor
{
    string Name { get; }
    ValueTask<string?> GetValueAsync(HttpContext context, CancellationToken cancellationToken = default);
}