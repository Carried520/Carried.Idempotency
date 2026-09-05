namespace Carried.Idempotency.AspNet.Options;

public sealed class IdempotencyAspNetOptions
{
    public bool StoreClientErrors { get; set; } = true;
}