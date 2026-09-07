using Carried.Idempotency.DependencyInjection;
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
        /// <see cref="IDatabase"/> resolved from the application service provider.
        /// </summary>
        /// <param name="timeProvider">
        /// The time provider to use, or <see langword="null"/> to use
        /// <see cref="TimeProvider.System"/>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when an idempotency provider has already been configured.
        /// </exception>
        public void UseRedis(TimeProvider? timeProvider = null)
        {
            builder.ConfigureProvider(serviceProvider =>
            {
                var database = serviceProvider.GetRequiredService<IDatabase>();
                return IdempotencyService.CreateRedis(
                    database,
                    builder.Options,
                    timeProvider);
            });
        }

        /// <summary>
        /// Configures Redis as the idempotency provider using the specified database.
        /// </summary>
        /// <param name="database">
        /// The Redis database used to coordinate idempotency state.
        /// </param>
        /// <param name="timeProvider">
        /// The time provider to use, or <see langword="null"/> to use
        /// <see cref="TimeProvider.System"/>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when an idempotency provider has already been configured.
        /// </exception>
        public void UseRedis(IDatabase database, TimeProvider? timeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(database);

            builder.ConfigureProvider(_ => IdempotencyService.CreateRedis(
                database,
                builder.Options,
                timeProvider));
        }
    }
}