namespace Carried.Idempotency.IdempotencyEvents;

public sealed record IdempotencyConflictEvent(
    IdempotencyKey Key);