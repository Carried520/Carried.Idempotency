using Carried.Idempotency.DependencyInjection;
using Carried.Idempotency.EntityFrameworkCore.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carried.Idempotency.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides Entity Framework Core persistence extensions for
/// <see cref="IdempotencyBuilder"/>.
/// </summary>
public static class IdempotencyBuilderExtensions
{
    extension(IdempotencyBuilder builder)
    {
        /// <summary>
        /// Configures Carried.Idempotency to persist idempotency entries
        /// using the specified Entity Framework Core <see cref="DbContext"/>.
        /// </summary>
        /// <typeparam name="TContext">
        /// The <see cref="DbContext"/> type used to persist idempotency entries.
        /// </typeparam>
        /// <returns>
        /// The same <see cref="IdempotencyBuilder"/> instance so that additional
        /// configuration can be chained.
        /// </returns>
        /// <remarks>
        /// <typeparamref name="TContext"/> must be registered with the application's
        /// dependency injection container. The context model must also register the
        /// idempotency entity configuration by calling <c>modelBuilder.AddIdempotency()</c>
        /// from <see cref="DbContext.OnModelCreating(ModelBuilder)"/>.
        /// </remarks>
        public IdempotencyBuilder UseDbContext<TContext>() where TContext : DbContext
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ConfigureProvider(serviceProvider =>
            {
                var context = serviceProvider.GetRequiredService<TContext>();
                TimeProvider timeProvider = serviceProvider.GetService<TimeProvider>()
                                            ?? TimeProvider.System;

                var store = new EntityFrameworkCoreStore<TContext>(context, builder.Options, timeProvider);

                return IdempotencyService.Create(store, builder.Options, timeProvider);
            });

            return builder;
        }
    }
}