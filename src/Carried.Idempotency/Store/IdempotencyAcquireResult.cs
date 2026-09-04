namespace Carried.Idempotency.Store;

public sealed record IdempotencyAcquireResult
{
    public IdempotencyAcquireStatus Status { get; }
    public Guid? OwnerToken { get; }
    public byte[]? Payload { get; }

    private IdempotencyAcquireResult(IdempotencyAcquireStatus status, Guid? ownerToken = null, byte[]? payload = null)
    {
        Status = status;
        OwnerToken = ownerToken;
        Payload = payload;
    }

    public static IdempotencyAcquireResult Acquired(Guid ownerToken) => new(IdempotencyAcquireStatus.Acquired, ownerToken);

    public static IdempotencyAcquireResult Completed(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new IdempotencyAcquireResult(IdempotencyAcquireStatus.Completed, payload: payload);
    }

    public static IdempotencyAcquireResult InProgress() => new(IdempotencyAcquireStatus.InProgress);
    public static IdempotencyAcquireResult Conflict() => new(IdempotencyAcquireStatus.Conflict);
}