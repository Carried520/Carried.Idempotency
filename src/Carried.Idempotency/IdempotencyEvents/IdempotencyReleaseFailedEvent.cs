namespace Carried.Idempotency.IdempotencyEvents;

/// <summary>
/// Represents an idempotency operation that failed to release ownership
/// of its key.
/// </summary>
/// <param name="Key">
/// The idempotency key whose ownership could not be released.
/// </param>
/// <param name="Exception">
/// The exception that caused the release attempt to fail.
/// </param>
public sealed record IdempotencyReleaseFailedEvent(
    IdempotencyKey Key,
    Exception Exception);