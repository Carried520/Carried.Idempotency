namespace Carried.Idempotency.IdempotencyOperation;

public sealed record IdempotencyOperationResult<T>
{
    internal IdempotencyOperationOutcome Outcome { get; }

    public T? Value { get; }

    private IdempotencyOperationResult(T? value, IdempotencyOperationOutcome outcome)
    {
        Value = value;
        Outcome = outcome;
    }

    public static IdempotencyOperationResult<T> Complete(T? value) => new(value, IdempotencyOperationOutcome.Complete);

    public static IdempotencyOperationResult<T> Release(T? value) => new(value, IdempotencyOperationOutcome.Release);
}