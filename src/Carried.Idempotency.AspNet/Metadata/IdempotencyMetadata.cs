namespace Carried.Idempotency.AspNet.Metadata;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class IdempotencyMetadata : Attribute
{
    public string? PolicyName { get; }

    public IdempotencyMetadata(string? policyName = null)
    {
        PolicyName = policyName;
    }
}