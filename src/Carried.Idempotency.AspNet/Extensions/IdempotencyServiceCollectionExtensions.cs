using Carried.Idempotency.AspNet.Errors;
using Carried.Idempotency.AspNet.Fingerprinting;
using Carried.Idempotency.AspNet.Observability;
using Carried.Idempotency.AspNet.Options;
using Carried.Idempotency.AspNet.Responses;
using Carried.Idempotency.AspNet.Validation;
using Carried.Idempotency.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
                .ValidateOnStart();

            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<IdempotencyAspNetOptions>, IdempotencyAspNetOptionsValidator>());

            if (configureAspNetOptions is not null)
            {
                services.Configure(configureAspNetOptions);
            }

            services.TryAddSingleton(idempotencyService);
            services.TryAddSingleton<IdempotentResponseExecutor>();

            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, IdempotencyEventLogger>());

            services.AddProblemDetails();
            services.TryAddSingleton<IIdempotencyErrorResponseWriter, IdempotencyErrorResponseWriter>();

            return services;
        }

        public IServiceCollection AddIdempotencyFingerprintContributor<T>() where T : class, IIdempotencyFingerprintContributor
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IIdempotencyFingerprintContributor, T>());
            return services;
        }

        public IServiceCollection AddIdempotencyFingerprintHeader(string headerName)
        {
            services.AddSingleton<IIdempotencyFingerprintContributor>(new HeaderFingerprintContributor(headerName));
            return services;
        }
    }
}