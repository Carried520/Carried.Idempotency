namespace Carried.Idempotency.Redis.Store;

internal enum RedisAcquireStatus
{
    Acquired = 0,
    InProgress = 1,
    Completed = 2,
    Conflict = 3
}