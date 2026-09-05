using Carried.Idempotency.AspNet.Fingerprinting;
using Carried.Idempotency.AspNet.Options;
using Carried.Idempotency.AspNet.Responses;
using Carried.Idempotency.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Carried.Idempotency.AspNet.Extensions;

public static class IdempotencyServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddIdempotency(Action<IdempotencyOptions>? configure = null, Action<IdempotencyAspNetOptions>? configureAspNetOptions = null)
        {
            var options = new IdempotencyOptions();

            configure?.Invoke(options);

            var idempotencyService = IdempotencyService.CreateInMemory(options);

            services.AddOptions<IdempotencyAspNetOptions>();
            
            if (configureAspNetOptions is not null)
            {
                services.Configure(configureAspNetOptions);
            }

            services.TryAddSingleton(idempotencyService);
            services.TryAddSingleton<IdempotentResponseExecutor>();

            return services;
        }
    }
}