using Carried.Idempotency.AspNet.Builders;
using Carried.Idempotency.AspNet.Errors;
using Carried.Idempotency.AspNet.Fingerprinting;
using Carried.Idempotency.AspNet.Observability;
using Carried.Idempotency.AspNet.Options;
using Carried.Idempotency.AspNet.Policies;
using Carried.Idempotency.AspNet.Responses;
using Carried.Idempotency.AspNet.Validation;
using Carried.Idempotency.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Carried.Idempotency.AspNet.Extensions;

/// <summary>
/// Provides registration extensions for ASP.NET Core idempotency services.
/// </summary>
public static class IdempotencyServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds idempotency services and configures the idempotency provider.
        /// </summary>
        /// <param name="configure">
        /// The delegate used to configure idempotency and select a provider.
        /// </param>
        /// <returns>The service collection.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no idempotency provider is configured or more than one provider is configured.
        /// </exception>
        public IServiceCollection AddIdempotency(
            Action<IdempotencyAspNetBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var coreOptions = new IdempotencyOptions();
            var aspNetOptions = new IdempotencyAspNetOptions();

            var builder = new IdempotencyAspNetBuilder(
                coreOptions,
                aspNetOptions);

            configure(builder);

            builder.EnsureProviderConfigured();

            services.TryAddSingleton(coreOptions);

            services.AddOptions<IdempotencyAspNetOptions>()
                .Configure(options =>
                    CopyAspNetOptions(aspNetOptions, options))
                .ValidateOnStart();

            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<IdempotencyAspNetOptions>,
                    IdempotencyAspNetOptionsValidator>());

            services.TryAddSingleton<IdempotencyService>(builder.CreateService);

            services.TryAddSingleton<IdempotentResponseExecutor>();

            services.AddProblemDetails();

            services.TryAddSingleton<
                IIdempotencyErrorResponseWriter,
                IdempotencyErrorResponseWriter>();

            return services;
        }

        /// <summary>
        /// Adds logging for idempotency lifecycle events.
        /// </summary>
        /// <returns>The service collection.</returns>
        public IServiceCollection AddIdempotencyLogging()
        {
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IHostedService,
                    IdempotencyEventLogger>());

            return services;
        }

        /// <summary>
        /// Adds metrics for idempotency lifecycle and HTTP response outcomes.
        /// </summary>
        /// <returns>The service collection.</returns>
        public IServiceCollection AddIdempotencyMetrics()
        {
            services.TryAddSingleton<IdempotencyMetrics>();

            services.TryAddSingleton<IIdempotencyMetricsRecorder>(static provider =>
                provider.GetRequiredService<IdempotencyMetrics>());

            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IHostedService,
                    IdempotencyMetricsHostedService>());

            return services;
        }

        /// <summary>
        /// Adds a fingerprint contributor used when computing request fingerprints.
        /// </summary>
        /// <typeparam name="T">
        /// The fingerprint contributor type.
        /// </typeparam>
        /// <returns>The service collection.</returns>
        public IServiceCollection AddIdempotencyFingerprintContributor<T>()
            where T : class, IIdempotencyFingerprintContributor
        {
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IIdempotencyFingerprintContributor,
                    T>());

            return services;
        }

        /// <summary>
        /// Adds an HTTP request header to the inputs used when computing request fingerprints.
        /// </summary>
        /// <param name="headerName">
        /// The name of the request header to include.
        /// </param>
        /// <returns>The service collection.</returns>
        public IServiceCollection AddIdempotencyFingerprintHeader(
            string headerName)
        {
            if (!HttpHeaderNameValidator.IsValid(headerName))
            {
                throw new ArgumentException(
                    $"'{headerName}' is not a valid HTTP header name.",
                    nameof(headerName));
            }

            services.AddSingleton<IIdempotencyFingerprintContributor>(new HeaderFingerprintContributor(headerName));

            return services;
        }
    }

    private static void CopyAspNetOptions(
        IdempotencyAspNetOptions source,
        IdempotencyAspNetOptions destination)
    {
        destination.HeaderName = source.HeaderName;

        CopyPolicy(source.DefaultPolicy, destination.DefaultPolicy);

        foreach ((string name, IdempotencyPolicy policy) in source.Policies)
        {
            destination.AddPolicy(
                name,
                destinationPolicy =>
                    CopyPolicy(policy, destinationPolicy));
        }
    }

    private static void CopyPolicy(
        IdempotencyPolicy source,
        IdempotencyPolicy destination)
    {
        destination.StoreClientErrors = source.StoreClientErrors;
        destination.MaxKeyLength = source.MaxKeyLength;
        destination.MaxRetainedResponseBodySize =
            source.MaxRetainedResponseBodySize;

        destination.ReplayHeaders.Clear();

        foreach (string header in source.ReplayHeaders)
        {
            destination.ReplayHeaders.Add(header);
        }
    }
}