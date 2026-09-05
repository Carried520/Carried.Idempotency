namespace Carried.Idempotency.IdempotencyEvents;

public sealed record IdempotencyReleasedEvent(
    IdempotencyKey Key);