namespace Carried.Idempotency.IdempotencyEvents;

public sealed record IdempotencyAcquiredEvent(
    IdempotencyKey Key);