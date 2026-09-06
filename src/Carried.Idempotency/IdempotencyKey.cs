namespace Carried.Idempotency;

/// <summary>
/// Identifies an idempotent operation within a scope.
/// </summary>
public sealed record IdempotencyKey
{
    /// <summary>
    /// Gets the scope in which the idempotency key is unique.
    /// </summary>
    public string Scope { get; }

    /// <summary>
    /// Gets the value of the IdempotencyKey.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyKey"/> record.
    /// </summary>
    /// <param name="scope">
    /// The scope in which the idempotency key is unique.
    /// </param>
    /// <param name="value">
    /// The value of the idempotency key.
    /// </param>
    public IdempotencyKey(string scope, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Scope = scope;
        Value = value;
    }
}