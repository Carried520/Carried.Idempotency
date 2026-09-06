namespace Carried.Idempotency.IdempotencyEvents;

/// <summary>
/// Represents an idempotency operation that encountered a key
/// currently owned by another operation.
/// </summary>
/// <param name="Key">
/// The idempotency key that is currently owned.
/// </param>
public sealed record IdempotencyInProgressEvent(
    IdempotencyKey Key);