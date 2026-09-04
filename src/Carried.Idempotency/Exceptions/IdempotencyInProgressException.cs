namespace Carried.Idempotency.Exceptions;

public sealed class IdempotencyInProgressException() : Exception("The idempotent operation is already in progress");