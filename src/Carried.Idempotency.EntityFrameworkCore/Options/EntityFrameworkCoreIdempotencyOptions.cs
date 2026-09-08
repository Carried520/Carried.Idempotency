namespace Carried.Idempotency.EntityFrameworkCore.Options;

public sealed class EntityFrameworkCoreIdempotencyOptions
{
    public string TableName { get; set; } = "__CarriedIdempotency";
    public string? Schema { get; set; }
}