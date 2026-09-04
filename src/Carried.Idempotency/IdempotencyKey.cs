namespace Carried.Idempotency;

public sealed record IdempotencyKey
{
    public string Scope { get; }
    public string Value { get; }

    public IdempotencyKey(string scope, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Scope = scope;
        Value = value;
    }
}