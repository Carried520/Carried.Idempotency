using Carried.Idempotency.Store;

namespace Carried.Idempotency.AspNet.Tests;

internal sealed class LeaseLosingIdempotencyStore :
    IIdempotencyStore
{
    private readonly Guid _ownerToken =
        Guid.NewGuid();

    private int _renewalAttempts;

    public int RenewalAttempts =>
        Volatile.Read(ref _renewalAttempts);

    public ValueTask<IdempotencyAcquireResult> TryAcquireAsync(
        IdempotencyKey key,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            IdempotencyAcquireResult.Acquired(
                _ownerToken));
    }

    public ValueTask<bool> TryCompleteAsync(
        IdempotencyKey key,
        Guid ownerToken,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(false);
    }

    public ValueTask<bool> TryReleaseAsync(
        IdempotencyKey key,
        Guid ownerToken,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> TryRenewLeaseAsync(
        IdempotencyKey key,
        Guid ownerToken,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(
            ref _renewalAttempts);
        
        return ValueTask.FromResult(false);
    }
}