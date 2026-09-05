using Carried.Idempotency.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Carried.Idempotency.AspNet;

internal static class IdempotencyExceptionMapper
{
    public static int GetStatusCode(Exception exception)
    {
        return exception switch
        {
            IdempotencyConflictException => StatusCodes.Status409Conflict,
            IdempotencyInProgressException => StatusCodes.Status409Conflict,
            IdempotencyLeaseLostException => StatusCodes.Status409Conflict,
            _ => throw new ArgumentOutOfRangeException(nameof(exception), exception, "Unsupported idempotency exception.")
        };
    }


    public static string GetErrorCode(Exception exception)
    {
        return exception switch
        {
            IdempotencyConflictException => "idempotency_conflict",
            IdempotencyInProgressException => "idempotency_in_progress",
            IdempotencyLeaseLostException => "idempotency_lease_lost",
            _ => throw new ArgumentOutOfRangeException(nameof(exception), exception, "Unsupported idempotency exception.")
        };
    }
}