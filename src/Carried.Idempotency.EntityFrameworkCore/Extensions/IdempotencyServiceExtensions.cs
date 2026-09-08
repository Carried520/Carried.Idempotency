using Carried.Idempotency.EntityFrameworkCore.Store;
using Carried.Idempotency.Options;
using Microsoft.EntityFrameworkCore;

namespace Carried.Idempotency.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides Entity Framework Core persistence extensions for
/// <see cref="IdempotencyService"/>.
/// </summary>
public static class IdempotencyServiceExtensions
{
    extension(IdempotencyService)
    {
        /// <summary>
        /// Creates an <see cref="IdempotencyService"/> that persists idempotency
        /// entries using the specified Entity Framework Core <see cref="DbContext"/>.
        /// </summary>
        /// <typeparam name="TContext">
        /// The <see cref="DbContext"/> type used to persist idempotency entries.
        /// </typeparam>
        /// <param name="context">
        /// The <see cref="DbContext"/> instance used by the idempotency store.
        /// </param>
        /// <param name="options">
        /// The idempotency options to use, or <see langword="null"/> to use
        /// the default options.
        /// </param>
        /// <param name="timeProvider">
        /// The time provider to use, or <see langword="null"/> to use
        /// <see cref="TimeProvider.System"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IdempotencyService"/> backed by Entity Framework Core.
        /// </returns>
        /// <remarks>
        /// The caller retains ownership of <paramref name="context"/> and is
        /// responsible for its lifetime. The context model must register the
        /// idempotency entity configuration by calling
        /// <c>modelBuilder.AddIdempotency()</c> from
        /// <see cref="DbContext.OnModelCreating(ModelBuilder)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
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