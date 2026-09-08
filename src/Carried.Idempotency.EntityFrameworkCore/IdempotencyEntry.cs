namespace Carried.Idempotency.EntityFrameworkCore;

internal sealed class IdempotencyEntry
{
    internal const int MaxKeyPartLength = 256;
    
    public string Key { get; set; } = null!;
    public string Scope { get; set; } = null!;

    public string Fingerprint { get; set; } = null!;
    public Guid? OwnerToken { get; set; }

    public IdempotencyEntryState State { get; set; }

    public byte[]? Payload { get; set; }

    public DateTime ExpiresAt { get; set; }
}