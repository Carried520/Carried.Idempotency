namespace Carried.Idempotency.Store;

public interface IIdempotencyStore
{
    /// <summary>
    /// Atomically attempts to acquire an ownership of the idempotency key.
    /// </summary>
    /// <remarks>
    /// Implementations must ensure that concurrent callers cannot both
    /// successfully acquire the same active key.
    ///
    /// Expired in-progress entries or completed entries may be replaced atomically.
    /// Fingerprint mismatches on active retained entries must return Conflict.
    /// </remarks>
    ValueTask<IdempotencyAcquireResult> TryAcquireAsync(IdempotencyKey key, string fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically completes an entry only if the supplied owner token
    /// still owns the active lease
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if entry was completed; otherwise,
    /// <see langword="false"/>
    /// </returns>
    ValueTask<bool> TryCompleteAsync(IdempotencyKey key, Guid ownerToken, byte[] payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically releases an in-progress entry only if the supplied
    /// owner token still owns the active lease.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if entry was released; otherwise,
    /// <see langword="false"/>
    /// </returns>
    ValueTask<bool> TryReleaseAsync(IdempotencyKey key, Guid ownerToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically renews the lease only if the supplied owner token
    /// still owns the active, unexpired entry.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if lease was renewed; otherwise,
    /// <see langword="false"/>
    /// </returns>
    ValueTask<bool> TryRenewLeaseAsync(IdempotencyKey key, Guid ownerToken, CancellationToken cancellationToken = default);
}