namespace Carried.Idempotency.IdempotencyEvents;

public sealed record IdempotencyReplayedEvent(
    IdempotencyKey Key);