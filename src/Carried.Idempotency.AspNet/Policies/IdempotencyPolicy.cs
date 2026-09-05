using Carried.Idempotency.AspNet.Responses;

namespace Carried.Idempotency.AspNet.Policies;

public sealed class IdempotencyPolicy
{
    public bool StoreClientErrors { get; set; } = true;
    public int MaxKeyLength { get; set; } = 255;
    public long MaxRetainedResponseBodySize { get; set; } = 1024 * 1024;
    public ISet<string> ReplayHeaders { get; } = new HashSet<string>(IdempotencyReplayHeaders.Allowed, StringComparer.OrdinalIgnoreCase);
}