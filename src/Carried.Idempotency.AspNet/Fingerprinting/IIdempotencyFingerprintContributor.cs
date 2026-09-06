using Microsoft.AspNetCore.Http;

namespace Carried.Idempotency.AspNet.Fingerprinting;

/// <summary>
/// Contributes application-specific data to an idempotency request fingerprint.
/// </summary>
public interface IIdempotencyFingerprintContributor
{
    /// <summary>
    /// Gets the stable name that identifies this contributor in the fingerprint.
    /// </summary>
    /// <remarks>
    /// Contributor names must be non-empty and unique using ordinal comparison.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the value to include in the fingerprint for the current request.
    /// </summary>
    /// <remarks>
    /// The returned value should deterministically represent the request data
    /// contributed by this instance.
    /// </remarks>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// The value to include, or <see langword="null"/> if the value is absent.
    /// An empty string represents a present value and is distinct from
    /// <see langword="null"/>.
    /// </returns>
    ValueTask<string?> GetValueAsync(HttpContext context, CancellationToken cancellationToken = default);
}