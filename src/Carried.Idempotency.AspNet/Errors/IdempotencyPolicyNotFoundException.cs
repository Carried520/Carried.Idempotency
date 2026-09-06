namespace Carried.Idempotency.AspNet.Errors;

internal sealed class IdempotencyPolicyNotFoundException(string policyName)
    : Exception($"Idempotency policy '{policyName}' is not configured.");