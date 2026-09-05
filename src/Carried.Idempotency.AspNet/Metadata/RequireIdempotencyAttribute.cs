namespace Carried.Idempotency.AspNet.Metadata;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireIdempotencyAttribute : IdempotencyMetadata
{
    public RequireIdempotencyAttribute()
    {
    }

    public RequireIdempotencyAttribute(string policyName) : base(policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
    }
}