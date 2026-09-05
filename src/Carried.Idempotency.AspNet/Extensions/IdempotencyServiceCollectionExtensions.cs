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

            services.AddOptions<IdempotencyAspNetOptions>()
                .Validate(aspNetOptions => !string.IsNullOrWhiteSpace(aspNetOptions.HeaderName), "HeaderName is required.")
                .Validate(aspNetOptions => aspNetOptions.MaxKeyLength > 0, "MaxKeyLength must be greater than zero.")
                .Validate(aspNetOptions => aspNetOptions.MaxRetainedResponseBodySize > 0, "MaxRetainedResponseBodySize must be greater than zero.")
                .ValidateOnStart();

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