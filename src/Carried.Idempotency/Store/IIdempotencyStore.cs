namespace Carried.Idempotency.Store;

/// <summary>
/// Defines storage operations for coordinating idempotent operation ownership,
/// completion, replay, and lease renewal.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Atomically attempts to acquire ownership of an idempotency key.
    /// </summary>
    /// <remarks>
    /// Implementations must ensure that concurrent callers cannot both
    /// successfully acquire the same active key.
    ///
    /// An expired in-progress or completed entry may be atomically replaced.
    /// A fingerprint mismatch for an unexpired entry must result in
    /// <see cref="IdempotencyAcquireStatus.Conflict"/>.
    /// </remarks>
    /// <param name="key">
    /// The idempotency key to acquire.
    /// </param>
    /// <param name="fingerprint">
    /// The fingerprint identifying the operation associated with the key.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The result of the acquisition attempt.
    /// </returns>
    ValueTask<IdempotencyAcquireResult> TryAcquireAsync(
        IdempotencyKey key,
        string fingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically completes an in-progress entry only if the supplied owner token
    /// still owns its active, unexpired lease.
    /// </summary>
    /// <param name="key">
    /// The idempotency key to complete.
    /// </param>
    /// <param name="ownerToken">
    /// The token identifying the current owner.
    /// </param>
    /// <param name="payload">
    /// The serialized result to retain for replay.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the entry was completed; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    ValueTask<bool> TryCompleteAsync(
        IdempotencyKey key,
        Guid ownerToken,
        byte[] payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically releases an in-progress entry only if the supplied owner token
    /// still owns its active, unexpired lease.
    /// </summary>
    /// <param name="key">
    /// The idempotency key to release.
    /// </param>
    /// <param name="ownerToken">
    /// The token identifying the current owner.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the entry was released; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    ValueTask<bool> TryReleaseAsync(
        IdempotencyKey key,
        Guid ownerToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically renews the lease of an in-progress entry only if the supplied
    /// owner token still owns its active, unexpired lease.
    /// </summary>
    /// <param name="key">
    /// The idempotency key whose lease is to be renewed.
    /// </param>
    /// <param name="ownerToken">
    /// The token identifying the current owner.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the lease was renewed; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    ValueTask<bool> TryRenewLeaseAsync(
        IdempotencyKey key,
        Guid ownerToken,
        CancellationToken cancellationToken = default);
}