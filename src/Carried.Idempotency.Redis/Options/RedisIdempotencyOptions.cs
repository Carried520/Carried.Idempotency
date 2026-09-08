namespace Carried.Idempotency.Redis.Options;

public sealed class RedisIdempotencyOptions
{
    public string KeyPrefix { get; set; } = "carried:idempotency:";
}