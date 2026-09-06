namespace Carried.Idempotency.AspNet.Responses;

internal enum ResponseRetentionOutcome
{
    Retained,
    ServerError,
    RequestTimeout,
    RateLimited,
    ClientErrorPolicy,
    ResponseTooLarge,
    UnsupportedResponse
}