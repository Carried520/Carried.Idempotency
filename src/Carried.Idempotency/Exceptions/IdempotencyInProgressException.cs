namespace Carried.Idempotency.Exceptions;

/// <summary>
/// The exception thrown when an operation using the same idempotency key is already in progress.
/// </summary>
public sealed class IdempotencyInProgressException() : Exception("The idempotent operation is already in progress.");