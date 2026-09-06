using Carried.Idempotency.AspNet.Policies;

namespace Carried.Idempotency.AspNet.Options;

public sealed class IdempotencyAspNetOptions
{
    private readonly Dictionary<string, IdempotencyPolicy> _policies =
        new(StringComparer.OrdinalIgnoreCase);

    public string HeaderName { get; set; } = "Idempotency-Key";
    public IdempotencyPolicy DefaultPolicy { get; } = new();

    internal IReadOnlyDictionary<string, IdempotencyPolicy> Policies => _policies;

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