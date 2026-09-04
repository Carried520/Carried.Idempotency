namespace Carried.Idempotency.Exceptions;

public sealed class IdempotencyLeaseLostException()
    : Exception("The idempotency lease was lost before the operation could be completed.");