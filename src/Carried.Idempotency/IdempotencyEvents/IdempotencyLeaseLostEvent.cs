namespace Carried.Idempotency.IdempotencyEvents;

public sealed record IdempotencyLeaseLostEvent(
    IdempotencyKey Key);