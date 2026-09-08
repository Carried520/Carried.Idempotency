using Carried.Idempotency.Options;
using Carried.Idempotency.Redis.Options;
using Carried.Idempotency.Redis.Store;
using StackExchange.Redis;

namespace Carried.Idempotency.Redis.Extensions;

/// <summary>
/// Provides Redis-backed creation methods for idempotency services.
/// </summary>
public static class IdempotencyServiceExtensions
{
    extension(IdempotencyService)
    {
        /// <summary>
        /// Creates an idempotency service backed by Redis.
        /// </summary>
        /// <param name="database">
        /// The Redis database used to coordinate idempotency state.
        /// </param>
        /// <param name="idempotencyOptions">
        /// The options that configure idempotency behavior.
        /// </param>
        /// <param name="redisIdempotencyOptions">
        /// The Redis-specific options used to configure key namespacing,
        /// or <see langword="null"/> to use the default options.
        /// </param>
        /// <param name="timeProvider">
        /// The time provider to use, or <see langword="null"/> to use
        /// <see cref="TimeProvider.System"/>.
        /// </param>
        /// <returns>
        /// A new Redis-backed idempotency service.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the lease duration or completed retention is less than
        /// one millisecond.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="database"/> or
        /// <paramref name="idempotencyOptions"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the Redis key prefix is null, empty, or consists only of white-space characters.
        /// </exception>
        public static IdempotencyService CreateRedis(
            IDatabase database,
            IdempotencyOptions idempotencyOptions,
            RedisIdempotencyOptions? redisIdempotencyOptions = null,
            TimeProvider? timeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(idempotencyOptions);
            
            redisIdempotencyOptions ??= new RedisIdempotencyOptions();

            ValidateOptions(idempotencyOptions, redisIdempotencyOptions);

            var redisStore = new RedisStore(database, idempotencyOptions, redisIdempotencyOptions);

            return IdempotencyService.Create(redisStore, idempotencyOptions, timeProvider);
        }
    }

    private static void ValidateOptions(IdempotencyOptions options, RedisIdempotencyOptions redisOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redisOptions.KeyPrefix);

        if (options.LeaseDuration < TimeSpan.FromMilliseconds(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.LeaseDuration),
                "Redis requires a lease duration of at least one millisecond.");
        }

        if (options.CompletedRetention < TimeSpan.FromMilliseconds(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.CompletedRetention),
                "Redis requires completed retention of at least one millisecond.");
        }
    }
}