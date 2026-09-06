namespace Carried.Idempotency.AspNet.Metadata;

/// <summary>
/// Represents endpoint metadata that enables idempotency and optionally selects
/// a named idempotency policy.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class IdempotencyMetadata : Attribute
{
    /// <summary>
    /// Gets the name of the idempotency policy applied to the endpoint,
    /// or <see langword="null"/> to use the default policy.
    /// </summary>
    public string? PolicyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyMetadata"/> class.
    /// </summary>
    /// <param name="policyName">
    /// The name of the idempotency policy to apply, or <see langword="null"/>
    /// to use the default policy.
    /// </param>
    public IdempotencyMetadata(string? policyName = null)
    {
        PolicyName = policyName;
    }
}