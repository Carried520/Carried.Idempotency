using Carried.Idempotency.Options;
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
        public static IdempotencyService CreateRedis(IDatabase database,
            IdempotencyOptions idempotencyOptions,
            TimeProvider? timeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(idempotencyOptions);

            ValidateOptions(idempotencyOptions);

            var redisStore = new RedisStore(database, idempotencyOptions);

            return IdempotencyService.Create(redisStore, idempotencyOptions, timeProvider);
        }
    }

    private static void ValidateOptions(IdempotencyOptions options)
    {
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