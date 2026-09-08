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
            /// <see cref="IConnectionMultiplexer"/> resolved from the application service provider.
            /// </summary>
            /// <remarks>
            /// Requires an <see cref="IConnectionMultiplexer"/> to be registered
            /// in the application service provider.
            /// </remarks>
            /// <param name="timeProvider">
            /// The time provider to use, or <see langword="null"/> to use
            /// <see cref="TimeProvider.System"/>.
            /// </param>
            /// <exception cref="InvalidOperationException">
            /// Thrown when an idempotency provider has already been configured,
            /// or when the Redis provider is created without a registered
            /// <see cref="IConnectionMultiplexer"/>.
            /// </exception>
            public void UseRedis(TimeProvider? timeProvider = null)
            {
                builder.ConfigureProvider(serviceProvider =>
                {
                    var connectionMultiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
                    IDatabase database = connectionMultiplexer.GetDatabase();

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