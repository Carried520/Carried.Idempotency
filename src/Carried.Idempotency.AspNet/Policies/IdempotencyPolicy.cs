using Carried.Idempotency.AspNet.Responses;

namespace Carried.Idempotency.AspNet.Policies;

/// <summary>
/// Configures idempotency behavior for HTTP requests and responses.
/// </summary>
public sealed class IdempotencyPolicy
{
    /// <summary>
    /// Gets or sets whether client error responses are retained for replay.
    /// </summary>
    public bool StoreClientErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed length of an idempotency key.
    /// </summary>
    public int MaxKeyLength { get; set; } = 255;

    /// <summary>
    /// Gets or sets the maximum response body size, in bytes, that may be retained for replay.
    /// </summary>
    public long MaxRetainedResponseBodySize { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets the response headers that are retained and replayed with completed responses.
    /// </summary>
    public ISet<string> ReplayHeaders { get; } = new HashSet<string>(
        IdempotencyReplayHeaders.Defaults,
        StringComparer.OrdinalIgnoreCase);
}