using Carried.Idempotency.AspNet.Fingerprinting;
using Carried.Idempotency.AspNet.Options;
using Carried.Idempotency.AspNet.Policies;
using Microsoft.Extensions.Options;

namespace Carried.Idempotency.AspNet.Validation;

public sealed class IdempotencyAspNetOptionsValidator
    : IValidateOptions<IdempotencyAspNetOptions>
{
    private readonly IEnumerable<IIdempotencyFingerprintContributor>
        _contributors;

    public IdempotencyAspNetOptionsValidator(
        IEnumerable<IIdempotencyFingerprintContributor> contributors)
    {
        _contributors = contributors;
    }

    public ValidateOptionsResult Validate(
        string? name,
        IdempotencyAspNetOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.HeaderName))
        {
            failures.Add(
                "HeaderName is required.");
        }
        else if (!HttpHeaderNameValidator.IsValid(
                     options.HeaderName))
        {
            failures.Add(
                $"HeaderName '{options.HeaderName}' is not a valid HTTP header name.");
        }

        failures.AddRange(
            IdempotencyPolicyValidator.Validate(
                options.DefaultPolicy,
                "DefaultPolicy"));

        foreach (KeyValuePair<string, IdempotencyPolicy> policy
                 in options.Policies)
        {
            if (string.IsNullOrWhiteSpace(policy.Key))
            {
                failures.Add(
                    "Named policy names cannot be empty or whitespace.");

                continue;
            }

            failures.AddRange(
                IdempotencyPolicyValidator.Validate(
                    policy.Value,
                    $"Policies['{policy.Key}']"));
        }

        string? contributorError =
            FingerprintContributorValidator.Validate(
                _contributors);

        if (contributorError is not null)
        {
            failures.Add(
                contributorError);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}