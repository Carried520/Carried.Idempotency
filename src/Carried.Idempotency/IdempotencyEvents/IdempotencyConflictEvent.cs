namespace Carried.Idempotency.IdempotencyEvents;

/// <summary>
/// Represents an idempotency key that was reused for a different operation.
/// </summary>
/// <param name="Key">
/// The idempotency key associated with the conflict.
/// </param>
public sealed record IdempotencyConflictEvent(
    IdempotencyKey Key);