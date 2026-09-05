using Carried.Idempotency.AspNet.Policies;

namespace Carried.Idempotency.AspNet.Options;

public sealed class IdempotencyAspNetOptions
{
    public string HeaderName { get; set; } = "Idempotency-Key";
    public IdempotencyPolicy DefaultPolicy { get; } = new();
    public IDictionary<string, IdempotencyPolicy> Policies { get; } = new Dictionary<string, IdempotencyPolicy>(StringComparer.OrdinalIgnoreCase);

    public void AddPolicy(string name, Action<IdempotencyPolicy> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var policy = new IdempotencyPolicy();
        
        configure(policy);

        if (!Policies.TryAdd(name, policy))
        {
            throw new InvalidOperationException($"The policy '{name}' is already configured.");
        }
    }
}