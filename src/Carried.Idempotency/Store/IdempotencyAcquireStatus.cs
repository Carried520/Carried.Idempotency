namespace Carried.Idempotency.Store;

/// <summary>
/// Represents the outcome of attempting to acquire an idempotency key.
/// </summary>
public enum IdempotencyAcquireStatus
{
    /// <summary>
    /// Ownership of the idempotency key was successfully acquired.
    /// </summary>
    Acquired,

    /// <summary>
    /// The idempotency key is currently owned by another operation.
    /// </summary>
    InProgress,

    /// <summary>
    /// The idempotency key represents a previously completed operation.
    /// </summary>
    Completed,

    /// <summary>
    /// The idempotency key is associated with a different operation.
    /// </summary>
    Conflict
}