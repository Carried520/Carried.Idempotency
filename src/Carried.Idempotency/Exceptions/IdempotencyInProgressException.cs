namespace Carried.Idempotency.Exceptions;

public sealed class IdempotencyInProgressException : Exception
{
    public IdempotencyInProgressException() : base("The idempotent operation is already in progress")
    {
    }
}