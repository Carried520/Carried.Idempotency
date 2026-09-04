namespace Carried.Idempotency.Store;

public record IdempotencyAcquireResult
{
    public required IdempotencyAcquireStatus Status { get; init; }
    public Guid? OwnerToken { get; init; }
    public byte[]? Payload { get; init; }
}