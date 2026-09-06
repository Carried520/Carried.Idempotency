namespace Carried.Idempotency.IdempotencyEvents;

/// <summary>
/// Represents an idempotency operation whose previously completed result
/// was replayed.
/// </summary>
/// <param name="Key">
/// The idempotency key whose completed result was replayed.
/// </param>
public sealed record IdempotencyReplayedEvent(
    IdempotencyKey Key);