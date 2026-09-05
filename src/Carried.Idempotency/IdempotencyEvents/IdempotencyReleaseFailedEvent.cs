namespace Carried.Idempotency.IdempotencyEvents;

public sealed record IdempotencyReleaseFailedEvent(
    IdempotencyKey Key,
    Exception? Exception = null);