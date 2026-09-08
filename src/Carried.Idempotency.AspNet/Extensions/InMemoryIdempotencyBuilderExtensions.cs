using Carried.Idempotency.AspNet.Builders;
using Carried.Idempotency.Options;

namespace Carried.Idempotency.AspNet.Extensions;

/// <summary>
/// Provides registration extensions for the in-memory idempotency provider.
/// </summary>
public static class InMemoryIdempotencyBuilderExtensions
{
    extension(IdempotencyBuilder builder)
    {
        /// <summary>
        /// Configures idempotency to use the in-memory provider.
        /// </summary>
        /// <remarks>
        /// Stored idempotency state is local to the application process and is not
        /// shared across application instances.
        /// </remarks>
        public void UseInMemory()
        {
            ArgumentNullException.ThrowIfNull(builder);

            IdempotencyOptions options = builder.CoreOptions;

            builder.ConfigureProvider(_ => IdempotencyService.CreateInMemory(options));
        }
    }
}