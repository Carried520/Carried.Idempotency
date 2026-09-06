namespace Carried.Idempotency.IdempotencyEvents;

/// <summary>
/// Represents an idempotency operation that was successfully completed
/// and retained for replay.
/// </summary>
/// <param name="Key">
/// The idempotency key whose operation was completed.
/// </param>
public sealed record IdempotencyCompletedEvent(
    IdempotencyKey Key);