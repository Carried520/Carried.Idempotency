namespace Carried.Idempotency.AspNet.Metadata;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class IdempotencyMetadata : Attribute;