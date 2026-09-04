namespace Carried.Idempotency.Exceptions;

/// <summary>
/// The exception thrown when ownership of an idempotency key is lost
/// before the operation can be completed.
/// </summary>
public sealed class IdempotencyLeaseLostException()
    : Exception("The idempotency lease was lost before the operation could be completed.");