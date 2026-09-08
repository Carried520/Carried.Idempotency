using Carried.Idempotency.DependencyInjection;
using Carried.Idempotency.Redis.Options;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Carried.Idempotency.Redis.Extensions;

/// <summary>
/// Provides Redis provider configuration for idempotency builders.
/// </summary>
public static class IdempotencyBuilderExtensions
{
    extension(IdempotencyBuilder builder)
    {
        /// <summary>
        /// Configures Redis as the idempotency provider using an
        /// <see cref="IConnectionMultiplexer"/> resolved from the application service provider.
        /// </summary>
        /// <remarks>
        /// Requires an <see cref="IConnectionMultiplexer"/> to be registered
        /// in the application service provider.
        /// </remarks>
        /// <param name="configure">
        /// An optional delegate used to configure Redis-specific idempotency options.
        /// </param>
        /// <param name="timeProvider">
        /// The time provider to use, or <see langword="null"/> to use
        /// <see cref="TimeProvider.System"/>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when an idempotency provider has already been configured,
        /// or when the Redis provider is created without a registered
        /// <see cref="IConnectionMultiplexer"/>.
        /// </exception>
        public void UseRedis(
            Action<RedisIdempotencyOptions>? configure = null,
            TimeProvider? timeProvider = null)
        {
            var redisOptions = new RedisIdempotencyOptions();
            configure?.Invoke(redisOptions);
            builder.ConfigureProvider(serviceProvider =>
            {
                var connectionMultiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
                IDatabase database = connectionMultiplexer.GetDatabase();

                return IdempotencyService.CreateRedis(
                    database,
                    builder.Options,
                    redisOptions,
                    timeProvider);
            });
        }

        /// <summary>
        /// Configures Redis as the idempotency provider using the specified database.
        /// </summary>
        /// <param name="database">
        /// The Redis database used to coordinate idempotency state.
        /// </param>
        /// <param name="configure">
        /// An optional delegate used to configure Redis-specific idempotency options.
        /// </param>
        /// <param name="timeProvider">
        /// The time provider to use, or <see langword="null"/> to use
        /// <see cref="TimeProvider.System"/>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when an idempotency provider has already been configured.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="database"/> is <see langword="null"/>.
        /// </exception>
        public void UseRedis(
            IDatabase database,
            Action<RedisIdempotencyOptions>? configure = null,
            TimeProvider? timeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(database);

            var redisOptions = new RedisIdempotencyOptions();
            configure?.Invoke(redisOptions);

            builder.ConfigureProvider(_ => IdempotencyService.CreateRedis(
                database,
                builder.Options,
                redisOptions,
                timeProvider));
        }
    }
}