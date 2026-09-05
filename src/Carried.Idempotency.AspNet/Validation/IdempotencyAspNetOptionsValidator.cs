using Carried.Idempotency.AspNet.Fingerprinting;
using Carried.Idempotency.AspNet.Options;
using Carried.Idempotency.AspNet.Policies;
using Microsoft.Extensions.Options;

namespace Carried.Idempotency.AspNet.Validation;

public class IdempotencyAspNetOptionsValidator : IValidateOptions<IdempotencyAspNetOptions>
{
    private readonly IEnumerable<IIdempotencyFingerprintContributor> _contributors;

    public IdempotencyAspNetOptionsValidator(IEnumerable<IIdempotencyFingerprintContributor> contributors)
    {
        _contributors = contributors;
    }

    public ValidateOptionsResult Validate(string? name, IdempotencyAspNetOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HeaderName))
        {
            return ValidateOptionsResult.Fail(
                "HeaderName is required.");
        }

        if (!IdempotencyPolicyValidator.IsValid(
                options.DefaultPolicy))
        {
            return ValidateOptionsResult.Fail(
                "Default idempotency policy is invalid.");
        }

        if (!options.Policies.Values.All(
                IdempotencyPolicyValidator.IsValid))
        {
            return ValidateOptionsResult.Fail(
                "One or more named idempotency policies are invalid.");
        }

        string? contributorError =
            FingerprintContributorValidator.Validate(
                _contributors);

        if (contributorError is not null)
        {
            return ValidateOptionsResult.Fail(
                contributorError);
        }

        return ValidateOptionsResult.Success;
    }
}