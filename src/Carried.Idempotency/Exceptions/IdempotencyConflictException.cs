namespace Carried.Idempotency.Exceptions;

public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException() : base("The idempotency key was already used for a different operation.")
    {
    }
}