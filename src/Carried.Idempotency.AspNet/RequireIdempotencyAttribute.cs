namespace Carried.Idempotency.AspNet;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireIdempotencyAttribute : IdempotencyMetadata
{
    
}