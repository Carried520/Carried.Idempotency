namespace Carried.Idempotency.Exceptions;

public sealed class IdempotencyConflictException() : Exception("The idempotency key was already used for a different operation.");