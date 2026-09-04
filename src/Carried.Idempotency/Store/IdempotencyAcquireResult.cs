namespace Carried.Idempotency.Store;

/// <summary>
/// Represents the result of attempting to acquire an idempotency key.
/// </summary>
public sealed record IdempotencyAcquireResult
{
    /// <summary>
    /// Gets an outcome of acquisition attempt.
    /// </summary>
    public IdempotencyAcquireStatus Status { get; }
    
    /// <summary>
    /// Gets the ownership token when <see cref="Status"/> is
    /// <see cref="IdempotencyAcquireStatus.Acquired"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public Guid? OwnerToken { get; }
    
    /// <summary>
    /// Gets the completed payload when <see cref="Status"/> is
    /// <see cref="IdempotencyAcquireStatus.Completed"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public byte[]? Payload { get; }

    private IdempotencyAcquireResult(IdempotencyAcquireStatus status, Guid? ownerToken = null, byte[]? payload = null)
    {
        Status = status;
        OwnerToken = ownerToken;
        Payload = payload;
    }

    /// <summary>
    /// Creates a result indicating that ownership of a key was acquired.
    /// </summary>
    public static IdempotencyAcquireResult Acquired(Guid ownerToken) => new(IdempotencyAcquireStatus.Acquired, ownerToken);

    /// <summary>
    /// Creates a result containing the payload of a previously completed operation.
    /// </summary>
    public static IdempotencyAcquireResult Completed(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new IdempotencyAcquireResult(IdempotencyAcquireStatus.Completed, payload: payload);
    }

    /// <summary>
    /// Creates a result indicating that the key is currently being processed.
    /// </summary>
    public static IdempotencyAcquireResult InProgress() => new(IdempotencyAcquireStatus.InProgress);
    
    /// <summary>
    /// Creates a result indicating that the key conflicts with an existing entry.
    /// </summary>
    public static IdempotencyAcquireResult Conflict() => new(IdempotencyAcquireStatus.Conflict);
}