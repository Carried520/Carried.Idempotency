namespace Carried.Idempotency.IdempotencyEvents;

public sealed record IdempotencyInProgressEvent(
    IdempotencyKey Key);