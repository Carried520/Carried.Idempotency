namespace Carried.Idempotency.AspNet.Options;

public sealed class IdempotencyAspNetOptions
{
    public bool StoreClientErrors { get; set; } = true;
    public string HeaderName { get; set; } = "Idempotency-Key";
    public int MaxKeyLength { get; set; } = 255;
    public long MaxResponseBodySize { get; set; }
}