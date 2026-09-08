using Carried.Idempotency.EntityFrameworkCore.Store;
using Carried.Idempotency.Options;
using Microsoft.EntityFrameworkCore;

namespace Carried.Idempotency.EntityFrameworkCore.Extensions;

public static class IdempotencyServiceExtensions
{
    extension(IdempotencyService)
    {
        public static IdempotencyService CreateEfCore<TContext>(TContext context,
            IdempotencyOptions? options = null,
            TimeProvider? timeProvider = null) where TContext : DbContext
        {
            ArgumentNullException.ThrowIfNull(context);

            options ??= new IdempotencyOptions();
            
            var store = new EntityFrameworkCoreStore<TContext>(context, options, timeProvider);

            return IdempotencyService.Create(store, options, timeProvider);
        }
    }
}