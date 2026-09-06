namespace Carried.Idempotency.IdempotencyEvents;

/// <summary>
/// Represents an idempotency operation that successfully released ownership
/// of its key without retaining a result for replay.
/// </summary>
/// <param name="Key">
/// The idempotency key whose ownership was released.
/// </param>
public sealed record IdempotencyReleasedEvent(
    IdempotencyKey Key);