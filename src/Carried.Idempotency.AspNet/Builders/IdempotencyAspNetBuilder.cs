using Carried.Idempotency.AspNet.Options;
using Carried.Idempotency.AspNet.Policies;
using Carried.Idempotency.DependencyInjection;
using Carried.Idempotency.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Carried.Idempotency.AspNet.Builders;

/// <summary>
/// Configures idempotency services, policies, and provider registration.
/// </summary>
public sealed class IdempotencyAspNetBuilder : IdempotencyBuilder
{
    private readonly IdempotencyAspNetOptions _aspNetOptions;

    internal IdempotencyOptions CoreOptions { get; }

    internal IdempotencyAspNetBuilder(
        IdempotencyOptions coreOptions,
        IdempotencyAspNetOptions aspNetOptions) : base(coreOptions)
    {
        ArgumentNullException.ThrowIfNull(coreOptions);
        ArgumentNullException.ThrowIfNull(aspNetOptions);

        CoreOptions = coreOptions;
        _aspNetOptions = aspNetOptions;
    }

    /// <summary>
    /// Gets or sets the duration for which an acquired idempotency key remains owned
    /// before its lease must be renewed.
    /// </summary>
    public TimeSpan LeaseDuration
    {
        get => CoreOptions.LeaseDuration;
        set => CoreOptions.LeaseDuration = value;
    }

    /// <summary>
    /// Gets or sets how long completed idempotency results are retained for replay.
    /// </summary>
    public TimeSpan CompletedRetention
    {
        get => CoreOptions.CompletedRetention;
        set => CoreOptions.CompletedRetention = value;
    }

    /// <summary>
    /// Gets or sets the HTTP header used to read the idempotency key.
    /// </summary>
    public string HeaderName
    {
        get => _aspNetOptions.HeaderName;
        set => _aspNetOptions.HeaderName = value;
    }

    /// <summary>
    /// Gets the policy applied to idempotent endpoints that do not specify a named policy.
    /// </summary>
    public IdempotencyPolicy DefaultPolicy => _aspNetOptions.DefaultPolicy;

    /// <summary>
    /// Adds a named idempotency policy.
    /// </summary>
    /// <param name="name">The policy name.</param>
    /// <param name="configure">The delegate used to configure the policy.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a policy with the same name has already been configured.
    /// </exception>
    public void AddPolicy(
        string name,
        Action<IdempotencyPolicy> configure)
    {
        _aspNetOptions.AddPolicy(name, configure);
    }
}