namespace Carried.Idempotency.IdempotencyOperation;

/// <summary>
/// Represents the result of an idempotent operation and determines
/// whether its value is retained for replay.
/// </summary>
/// <typeparam name="T">
/// The type of value produced by the operation.
/// </typeparam>
public sealed record IdempotencyOperationResult<T>
{
    internal IdempotencyOperationOutcome Outcome { get; }

    /// <summary>
    /// Gets the value produced by the operation.
    /// </summary>
    public T? Value { get; }

    private IdempotencyOperationResult(T? value, IdempotencyOperationOutcome outcome)
    {
        Value = value;
        Outcome = outcome;
    }

    /// <summary>
    /// Creates a result whose value is retained for replay.
    /// </summary>
    /// <param name="value">
    /// The value produced by the operation.
    /// </param>
    /// <returns>
    /// A result that completes the idempotency entry.
    /// </returns>
    public static IdempotencyOperationResult<T> Complete(T? value) => new(value, IdempotencyOperationOutcome.Complete);

    /// <summary>
    /// Creates a result whose value is returned without being retained for replay.
    /// </summary>
    /// <param name="value">
    /// The value produced by the operation.
    /// </param>
    /// <returns>
    /// A result that releases the idempotency entry.
    /// </returns>
    public static IdempotencyOperationResult<T> Release(T? value) => new(value, IdempotencyOperationOutcome.Release);
}