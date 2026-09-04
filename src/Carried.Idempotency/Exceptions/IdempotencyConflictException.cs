namespace Carried.Idempotency.Exceptions;

/// <summary>
/// The exception thrown when an idempotency key is reused for a different operation.
/// </summary>
public sealed class IdempotencyConflictException() : Exception("The idempotency key was already used for a different operation.");