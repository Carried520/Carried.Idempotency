namespace Carried.Idempotency;

public enum IdempotencyAcquireStatus
{
    Acquired,
    InProgress,
    Completed,
    Conflict
}