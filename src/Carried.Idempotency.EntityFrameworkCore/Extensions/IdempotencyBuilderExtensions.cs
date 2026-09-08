using Carried.Idempotency.DependencyInjection;
using Carried.Idempotency.EntityFrameworkCore.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carried.Idempotency.EntityFrameworkCore.Extensions;

public static class IdempotencyBuilderExtensions
{
    extension(IdempotencyBuilder builder)
    {
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