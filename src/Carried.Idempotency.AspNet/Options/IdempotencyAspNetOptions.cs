using Carried.Idempotency.AspNet.Policies;

namespace Carried.Idempotency.AspNet.Options;

/// <summary>
/// Configures the ASP.NET Core idempotency integration.
/// </summary>
public sealed class IdempotencyAspNetOptions
{
    private readonly Dictionary<string, IdempotencyPolicy> _policies =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the request header used to read the idempotency key.
    /// </summary>
    public string HeaderName { get; set; } = "Idempotency-Key";

    /// <summary>
    /// Gets the default idempotency policy used when no named policy is selected.
    /// </summary>
    public IdempotencyPolicy DefaultPolicy { get; } = new();

    internal IReadOnlyDictionary<string, IdempotencyPolicy> Policies => _policies;

    /// <summary>
    /// Adds a named idempotency policy.
    /// </summary>
    /// <param name="name">The unique policy name.</param>
    /// <param name="configure">The action used to configure the policy.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a policy with the same name has already been configured.
    /// </exception>
    public void AddPolicy(string name, Action<IdempotencyPolicy> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var policy = new IdempotencyPolicy();

        configure(policy);

        if (!_policies.TryAdd(name, policy))
        {
            throw new InvalidOperationException($"The policy '{name}' is already configured.");
        }
    }
}