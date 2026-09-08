namespace Carried.Idempotency.EntityFrameworkCore.Store;

internal enum IdempotencyEntryState
{
    InProgress,
    Completed
}