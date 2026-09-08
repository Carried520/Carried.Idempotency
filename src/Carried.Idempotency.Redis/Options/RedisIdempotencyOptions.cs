namespace Carried.Idempotency.Redis.Options;

/// <summary>
/// Provides configuration options for the Redis idempotency provider.
/// </summary>
public sealed class RedisIdempotencyOptions
{
    /// <summary>
    /// Gets or sets the prefix applied to Redis keys created by the idempotency provider.
    /// </summary>
    /// <remarks>
    /// Use different prefixes to isolate idempotency state between applications
    /// that share the same Redis database.
    /// </remarks>
    public string KeyPrefix { get; set; } = "carried:idempotency:";
}