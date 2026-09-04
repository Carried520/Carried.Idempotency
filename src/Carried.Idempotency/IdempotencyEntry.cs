namespace Carried.Idempotency;

public sealed record IdempotencyEntry
{
    public required string Fingerprint { get; init; }
    public required IdempotencyState State { get; init; }

    public byte[]? Payload { get; init; }
    public Guid? OwnerToken { get; init; }
}