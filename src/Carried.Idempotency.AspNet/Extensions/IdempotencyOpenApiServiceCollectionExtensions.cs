using Carried.Idempotency.AspNet.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace Carried.Idempotency.AspNet.Extensions;

/// <summary>
/// Provides extension methods for adding idempotency support to ASP.NET Core OpenAPI generation.
/// </summary>
public static class IdempotencyOpenApiServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds OpenAPI support for endpoints that require idempotency.
        /// </summary>
        /// <returns>The service collection.</returns>
        public IServiceCollection AddIdempotencyOpenApi()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddOpenApi(options =>
            {
                options.AddOperationTransformer<IdempotencyOpenApiOperationTransformer>();
            });

            return services;
        }
    }
}