using Carried.Idempotency.AspNet.Validation;

namespace Carried.Idempotency.AspNet.Policies;

internal static class IdempotencyPolicyValidator
{
    internal static IEnumerable<string> Validate(
        IdempotencyPolicy policy,
        string path)
    {
        if (policy.MaxKeyLength <= 0)
        {
            yield return
                $"{path}.MaxKeyLength must be greater than zero.";
        }

        if (policy.MaxRetainedResponseBodySize < 0)
        {
            yield return
                $"{path}.MaxRetainedResponseBodySize cannot be negative.";
        }

        foreach (string replayHeader in policy.ReplayHeaders)
        {
            if (string.IsNullOrWhiteSpace(replayHeader))
            {
                yield return
                    $"{path}.ReplayHeaders cannot contain empty or whitespace header names.";

                continue;
            }

            if (!HttpHeaderNameValidator.IsValid(
                    replayHeader))
            {
                yield return
                    $"{path}.ReplayHeaders contains invalid HTTP header name '{replayHeader}'.";
            }
        }
    }
}