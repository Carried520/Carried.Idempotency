namespace Carried.Idempotency.AspNet;

internal static class IdempotencyReplayHeaders
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "Location",
        "ETag",
        "Cache-Control",
        "Last-Modified",
    };
}