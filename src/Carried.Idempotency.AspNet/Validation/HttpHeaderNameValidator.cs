namespace Carried.Idempotency.AspNet.Validation;

internal static class HttpHeaderNameValidator
{
    internal static bool IsValid(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (char character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
                continue;

            if (character is
                '!' or '#' or '$' or '%' or '&' or '\'' or
                '*' or '+' or '-' or '.' or '^' or '_' or
                '`' or '|' or '~')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}