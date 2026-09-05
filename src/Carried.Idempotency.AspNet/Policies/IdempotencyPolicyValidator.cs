namespace Carried.Idempotency.AspNet.Policies;

internal static class IdempotencyPolicyValidator
{
    public static bool IsValid(IdempotencyPolicy policy)
    {
        return policy is { MaxKeyLength: > 0, MaxRetainedResponseBodySize: > 0 };
    }
}