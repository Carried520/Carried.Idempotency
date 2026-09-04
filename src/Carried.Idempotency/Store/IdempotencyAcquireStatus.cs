namespace Carried.Idempotency.Store;

public enum IdempotencyAcquireStatus
{
    Acquired,
    InProgress,
    Completed,
    Conflict
}