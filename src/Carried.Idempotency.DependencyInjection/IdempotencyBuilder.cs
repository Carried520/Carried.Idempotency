using Carried.Idempotency.Options;

namespace Carried.Idempotency.DependencyInjection;

public abstract class IdempotencyBuilder
{
    private Func<IServiceProvider, IdempotencyService>? _serviceFactory;

    /// <summary>
    /// Gets the options that configure idempotency behavior.
    /// </summary>
    public IdempotencyOptions Options { get; }

    /// <summary>
    /// Initializes a new idempotency builder.
    /// </summary>
    /// <param name="options">
    /// The options that configure idempotency behavior.
    /// </param>
    protected IdempotencyBuilder(IdempotencyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
    }

    /// <summary>
    /// Configures the provider used to create the idempotency service.
    /// </summary>
    /// <param name="factory">
    /// A factory that creates the idempotency service from the application service provider.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an idempotency provider has already been configured.
    /// </exception>
    public void ConfigureProvider(
        Func<IServiceProvider, IdempotencyService> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (_serviceFactory is not null)
        {
            throw new InvalidOperationException("An idempotency provider has already been configured.");
        }

        _serviceFactory = factory;
    }

    /// <summary>
    /// Ensures that an idempotency provider has been configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no idempotency provider has been configured.
    /// </exception>
    public void EnsureProviderConfigured()
    {
        if (_serviceFactory is null)
        {
            throw new InvalidOperationException("No idempotency provider has been configured.");
        }
    }

    /// <summary>
    /// Creates the configured idempotency service.
    /// </summary>
    /// <param name="serviceProvider">
    /// The application service provider passed to the configured provider factory.
    /// </param>
    /// <returns>
    /// The configured idempotency service.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no idempotency provider has been configured.
    /// </exception>
    public IdempotencyService CreateService(
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        return _serviceFactory?.Invoke(serviceProvider)
               ?? throw new InvalidOperationException("No idempotency provider has been configured.");
    }
}