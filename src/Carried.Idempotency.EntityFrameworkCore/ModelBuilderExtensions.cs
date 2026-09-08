using Carried.Idempotency.EntityFrameworkCore.Options;
using Microsoft.EntityFrameworkCore;

namespace Carried.Idempotency.EntityFrameworkCore;

public static class ModelBuilderExtensions
{
    extension(ModelBuilder modelBuilder)
    {
        public ModelBuilder AddIdempotency(Action<EntityFrameworkCoreIdempotencyOptions>? configure = null)
        {
            var options = new EntityFrameworkCoreIdempotencyOptions();
            configure?.Invoke(options);

            if (string.IsNullOrWhiteSpace(options.TableName))
                throw new ArgumentException("Table name cannot be empty.", nameof(options));

            modelBuilder.ApplyConfiguration(new IdempotencyEntryConfiguration(options));

            return modelBuilder;
        }
    }
}