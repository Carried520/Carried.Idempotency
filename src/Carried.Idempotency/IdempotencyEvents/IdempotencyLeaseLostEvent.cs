namespace Carried.Idempotency.IdempotencyEvents;

/// <summary>
/// Represents an idempotency operation that lost ownership of its key
/// before it could be completed.
/// </summary>
/// <param name="Key">
/// The idempotency key whose ownership was lost.
/// </param>
public sealed record IdempotencyLeaseLostEvent(
    IdempotencyKey Key);