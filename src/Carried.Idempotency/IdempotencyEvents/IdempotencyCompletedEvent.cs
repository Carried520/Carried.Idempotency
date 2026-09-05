namespace Carried.Idempotency.IdempotencyEvents;

public sealed record IdempotencyCompletedEvent(
    IdempotencyKey Key);