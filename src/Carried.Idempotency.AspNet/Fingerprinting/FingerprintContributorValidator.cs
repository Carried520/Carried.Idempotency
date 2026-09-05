namespace Carried.Idempotency.AspNet.Fingerprinting;

internal static class FingerprintContributorValidator
{
    internal static string? Validate(IEnumerable<IIdempotencyFingerprintContributor> contributors)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (IIdempotencyFingerprintContributor contributor in contributors)
        {
            if (string.IsNullOrWhiteSpace(contributor.Name))
                return "Contributor name cannot be null or whitespace.";

            if (!names.Add(contributor.Name))
                return $"Fingerprint contributor name '{contributor.Name}' is already configured.";
        }

        return null;
    }
}