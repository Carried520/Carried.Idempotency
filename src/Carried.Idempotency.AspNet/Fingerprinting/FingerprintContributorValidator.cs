namespace Carried.Idempotency.AspNet.Fingerprinting;

internal static class FingerprintContributorValidator
{
    internal static IEnumerable<string> Validate(IEnumerable<IIdempotencyFingerprintContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (IIdempotencyFingerprintContributor contributor in contributors)
        {
            if (string.IsNullOrWhiteSpace(contributor.Name))
            {
                yield return "Contributor name cannot be null or whitespace.";
                continue;
            }

            if (!names.Add(contributor.Name))
                yield return $"Fingerprint contributor name '{contributor.Name}' is already configured.";
        }
    }
}