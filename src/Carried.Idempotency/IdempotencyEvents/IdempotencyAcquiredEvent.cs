namespace Carried.Idempotency.IdempotencyEvents;

/// <summary>
/// Represents an idempotency operation that successfully acquired ownership
/// of an idempotency key.
/// </summary>
/// <param name="Key">
/// The idempotency key whose ownership was acquired.
/// </param>
public sealed record IdempotencyAcquiredEvent(
    IdempotencyKey Key);