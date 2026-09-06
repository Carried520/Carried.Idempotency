namespace Carried.Idempotency.AspNet.Errors;

internal class UnsupportedIdempotentResponseException() : Exception("The idempotent endpoint produced a response that cannot be safely captured and replayed.");