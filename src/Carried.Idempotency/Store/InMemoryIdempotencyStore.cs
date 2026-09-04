using System.Collections.Concurrent;
using Carried.Idempotency.Options;

namespace Carried.Idempotency.Store;

internal sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly IdempotencyOptions _idempotencyOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<IdempotencyKey, IdempotencyEntry> _entries = new();

    public InMemoryIdempotencyStore(IdempotencyOptions idempotencyOptions, TimeProvider timeProvider)
    {
        _idempotencyOptions = idempotencyOptions;
        _timeProvider = timeProvider;
    }

    public ValueTask<IdempotencyAcquireResult> TryAcquireAsync(IdempotencyKey key, string fingerprint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        while (true)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            var ownerToken = Guid.NewGuid();
            var candidateEntry = new IdempotencyEntry
            {
                Fingerprint = fingerprint,
                State = IdempotencyState.InProgress,
                OwnerToken = ownerToken,
                Payload = null,
                LeaseExpiresAt = now + _idempotencyOptions.LeaseDuration,
                CompletedExpiresAt = null
            };

            if (_entries.TryAdd(key, candidateEntry))
            {
                return ValueTask.FromResult(
                    IdempotencyAcquireResult.Acquired(ownerToken));
            }

            if (!_entries.TryGetValue(key, out IdempotencyEntry? existingEntry))
            {
                continue;
            }

            switch (existingEntry.State)
            {
                case IdempotencyState.InProgress:
                    if (existingEntry.Fingerprint != fingerprint)
                        return ValueTask.FromResult(IdempotencyAcquireResult.Conflict());
                    if (existingEntry.LeaseExpiresAt is null)
                        throw new InvalidOperationException("LeaseExpiresAt is null");

                    if (now < existingEntry.LeaseExpiresAt)
                        return ValueTask.FromResult(IdempotencyAcquireResult.InProgress());

                    if (!_entries.TryUpdate(key, candidateEntry, existingEntry))
                        continue;

                    return ValueTask.FromResult(IdempotencyAcquireResult.Acquired(ownerToken));

                case IdempotencyState.Completed:
                    if (existingEntry.CompletedExpiresAt is null)
                        throw new InvalidOperationException("CompletedExpiresAt is null");

                    if (existingEntry.Payload is null)
                        throw new InvalidOperationException("Payload is null");

                    if (now < existingEntry.CompletedExpiresAt)
                    {
                        if (existingEntry.Fingerprint != fingerprint)
                            return ValueTask.FromResult(IdempotencyAcquireResult.Conflict());

                        return ValueTask.FromResult(IdempotencyAcquireResult.Completed(existingEntry.Payload));
                    }

                    if (!_entries.TryUpdate(key, candidateEntry, existingEntry))
                        continue;

                    return ValueTask.FromResult(IdempotencyAcquireResult.Acquired(ownerToken));

                default:
                    throw new InvalidOperationException();
            }
        }
    }

    public ValueTask<bool> TryCompleteAsync(IdempotencyKey key, Guid ownerToken, byte[] payload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_entries.TryGetValue(key, out IdempotencyEntry? existingEntry) || existingEntry.State is not IdempotencyState.InProgress ||
            existingEntry.OwnerToken != ownerToken)
        {
            return ValueTask.FromResult(false);
        }

        if (existingEntry.LeaseExpiresAt is null)
            throw new InvalidOperationException("LeaseExpiresAt is null");

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (now >= existingEntry.LeaseExpiresAt)
            return ValueTask.FromResult(false);

        var completedEntry = new IdempotencyEntry
        {
            Fingerprint = existingEntry.Fingerprint,
            State = IdempotencyState.Completed,
            OwnerToken = null,
            Payload = payload,
            LeaseExpiresAt = null,
            CompletedExpiresAt = now + _idempotencyOptions.CompletedRetention
        };

        return ValueTask.FromResult(_entries.TryUpdate(key, completedEntry, existingEntry));
    }


    public ValueTask<bool> TryReleaseAsync(IdempotencyKey key, Guid ownerToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_entries.TryGetValue(key, out IdempotencyEntry? existingEntry) ||
            existingEntry.State is not IdempotencyState.InProgress ||
            existingEntry.OwnerToken != ownerToken)
        {
            return ValueTask.FromResult(false);
        }

        if (existingEntry.LeaseExpiresAt is null)
            throw new InvalidOperationException("LeaseExpiresAt is null");

        if (_timeProvider.GetUtcNow() >= existingEntry.LeaseExpiresAt)
            return ValueTask.FromResult(false);

        return ValueTask.FromResult(_entries.TryRemove(new KeyValuePair<IdempotencyKey, IdempotencyEntry>(key, existingEntry)));
    }

    public ValueTask<bool> TryRenewLeaseAsync(IdempotencyKey key, Guid ownerToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_entries.TryGetValue(key, out IdempotencyEntry? existingEntry) || existingEntry.State is not IdempotencyState.InProgress ||
            ownerToken != existingEntry.OwnerToken)
            return ValueTask.FromResult(false);

        if (existingEntry.LeaseExpiresAt is null)
            throw new InvalidOperationException("LeaseExpiresAt is null");

        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (now >= existingEntry.LeaseExpiresAt)
            return ValueTask.FromResult(false);

        var renewedEntry = new IdempotencyEntry
        {
            Fingerprint = existingEntry.Fingerprint, State = IdempotencyState.InProgress, OwnerToken = existingEntry.OwnerToken, Payload = null,
            LeaseExpiresAt = now + _idempotencyOptions.LeaseDuration,
            CompletedExpiresAt = null
        };

        return ValueTask.FromResult(_entries.TryUpdate(key, renewedEntry, existingEntry));
    }
}