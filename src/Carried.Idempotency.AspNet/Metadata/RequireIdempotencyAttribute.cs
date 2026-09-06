namespace Carried.Idempotency.AspNet.Metadata;

/// <summary>
/// Marks a controller or action as requiring idempotency handling.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireIdempotencyAttribute : IdempotencyMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequireIdempotencyAttribute"/> class
    /// using the default idempotency policy.
    /// </summary>
    public RequireIdempotencyAttribute()
    {
    }

    /// <summary>
    /// Initializes the new instance of the <see cref="RequireIdempotencyAttribute"/> class
    /// using the specified named idempotency policy.
    /// </summary>
    /// <param name="policyName">The name of idempotency policy to apply.</param>
    public RequireIdempotencyAttribute(string policyName) : base(policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
    }
}