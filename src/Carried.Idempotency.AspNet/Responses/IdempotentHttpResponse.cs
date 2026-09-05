namespace Carried.Idempotency.AspNet.Responses;

internal sealed record IdempotentHttpResponse(
    int StatusCode,
    string? ContentType,
    Dictionary<string, string[]> Headers,
    byte[] Body);