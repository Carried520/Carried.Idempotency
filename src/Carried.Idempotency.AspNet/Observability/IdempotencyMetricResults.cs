namespace Carried.Idempotency.AspNet.Observability;

internal static class IdempotencyMetricResults
{
    internal const string Retained = "retained";

    internal const string ServerError = "server_error";

    internal const string RequestTimeout = "request_timeout";

    internal const string RateLimited = "rate_limited";

    internal const string ClientErrorPolicy = "client_error_policy";

    internal const string ResponseTooLarge = "response_too_large";

    internal const string UnsupportedResponse = "unsupported_response";
}