namespace Carried.Idempotency.Store;


public interface IIdempotencyStore
{
    ValueTask<IdempotencyAcquireResult> TryAcquireAsync(IdempotencyKey key, string fingerprint , CancellationToken cancellationToken = default);
    ValueTask<bool> TryCompleteAsync(IdempotencyKey key, Guid ownerToken, byte[] payload, CancellationToken cancellationToken = default);
    ValueTask<bool> TryReleaseAsync(IdempotencyKey key, Guid ownerToken, CancellationToken cancellationToken = default);
    ValueTask<bool> TryRenewLeaseAsync(IdempotencyKey key, Guid ownerToken, CancellationToken cancellationToken = default);
}