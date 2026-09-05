using Carried.Idempotency.AspNet.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace Carried.Idempotency.AspNet.Extensions;

public static class IdempotencyOpenApiServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
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