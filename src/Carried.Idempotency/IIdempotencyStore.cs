namespace Carried.Idempotency;

public interface IIdempotencyStore
{
    public IdempotencyAcquireResult TryAcquire(IdempotencyKey key, string fingerprint);
    public bool TryComplete(IdempotencyKey key, Guid ownerToken, byte[] payload);
    public bool TryRelease(IdempotencyKey key, Guid ownerToken);
}