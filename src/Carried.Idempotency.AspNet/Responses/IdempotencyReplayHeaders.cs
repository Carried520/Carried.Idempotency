namespace Carried.Idempotency.AspNet.Responses;

internal static class IdempotencyReplayHeaders
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Location",
        "ETag",
        "Cache-Control",
        "Last-Modified",
    };
}