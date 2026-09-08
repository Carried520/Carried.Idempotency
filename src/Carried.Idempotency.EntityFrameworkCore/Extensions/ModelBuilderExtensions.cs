using Carried.Idempotency.EntityFrameworkCore.Options;
using Carried.Idempotency.EntityFrameworkCore.Store;
using Microsoft.EntityFrameworkCore;

namespace Carried.Idempotency.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides Entity Framework Core model configuration extensions for
/// Carried.Idempotency.
/// </summary>
public static class ModelBuilderExtensions
{
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Adds the Entity Framework Core model configuration required by
        /// Carried.Idempotency.
        /// </summary>
        /// <param name="configure">
        /// An optional delegate used to configure the idempotency table.
        /// </param>
        /// <returns>
        /// The same <see cref="ModelBuilder"/> instance so that additional
        /// model configuration can be chained.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelBuilder"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the configured table name is empty or consists only of
        /// whitespace.
        /// </exception>
        public ModelBuilder AddIdempotency(Action<EntityFrameworkCoreIdempotencyOptions>? configure = null)
        {
            var options = new EntityFrameworkCoreIdempotencyOptions();
            configure?.Invoke(options);

            if (string.IsNullOrWhiteSpace(options.TableName))
                throw new ArgumentException("The idempotency table name cannot be empty or whitespace.");

            modelBuilder.ApplyConfiguration(new IdempotencyEntryConfiguration(options));

            return modelBuilder;
        }
    }
}