namespace Carried.Idempotency.Store;

internal sealed class IdempotencyEntry
{
    public required string Fingerprint { get; init; }
    public required IdempotencyState State { get; init; }

    public byte[]? Payload { get; init; }
    public Guid? OwnerToken { get; init; }

    public DateTimeOffset? LeaseExpiresAt { get; init; }
    public DateTimeOffset? CompletedExpiresAt { get; init; }
}