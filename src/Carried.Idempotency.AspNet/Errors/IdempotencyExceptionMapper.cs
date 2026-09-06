using Carried.Idempotency.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Carried.Idempotency.AspNet.Errors;

internal static class IdempotencyExceptionMapper
{
    internal static IdempotencyError Map(Exception exception)
    {
        return exception switch
        {
            IdempotencyConflictException => new IdempotencyError(StatusCodes.Status409Conflict, "idempotency_conflict", exception.Message),
            IdempotencyInProgressException => new IdempotencyError(StatusCodes.Status409Conflict, "idempotency_in_progress", exception.Message),
            IdempotencyLeaseLostException => new IdempotencyError(StatusCodes.Status409Conflict, "idempotency_lease_lost", exception.Message),
            UnsupportedIdempotentResponseException => new IdempotencyError(StatusCodes.Status500InternalServerError, "idempotency_unsupported_response",
                exception.Message),
            _ => throw new ArgumentOutOfRangeException(nameof(exception), exception, "Unsupported idempotency exception.")
        };
    }
}