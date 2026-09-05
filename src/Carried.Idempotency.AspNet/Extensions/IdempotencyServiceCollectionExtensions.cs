using Carried.Idempotency.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Carried.Idempotency.AspNet.Extensions;

public static class IdempotencyServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddIdempotency(Action<IdempotencyOptions>? configure = null)
        {
            var options = new IdempotencyOptions();

            configure?.Invoke(options);

            var idempotencyService = IdempotencyService.CreateInMemory(options);

            services.TryAddSingleton(idempotencyService);

            services.TryAddSingleton<RequestFingerprintProvider>();
            services.TryAddSingleton<IdempotentResponseExecutor>();

            return services;
        }
    }
}