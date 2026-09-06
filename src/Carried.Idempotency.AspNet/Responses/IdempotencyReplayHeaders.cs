namespace Carried.Idempotency.AspNet.Responses;

internal static class IdempotencyReplayHeaders
{
    internal static readonly IReadOnlySet<string> Defaults = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Location",
        "ETag",
        "Cache-Control",
        "Last-Modified",
    };
}