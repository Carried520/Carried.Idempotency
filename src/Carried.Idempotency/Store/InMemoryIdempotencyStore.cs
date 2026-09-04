using System.Collections.Concurrent;

namespace Carried.Idempotency.Store;

internal sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<IdempotencyKey, IdempotencyEntry> _entries = new();

    public ValueTask<IdempotencyAcquireResult> TryAcquireAsync(IdempotencyKey key, string fingerprint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        while (true)
        {
            var entry = new IdempotencyEntry
            {
                Fingerprint = fingerprint,
                State = IdempotencyState.InProgress,
                OwnerToken = Guid.NewGuid(),
                Payload = null
            };

            if (_entries.TryAdd(key, entry))
            {
                return ValueTask.FromResult(new IdempotencyAcquireResult { Status = IdempotencyAcquireStatus.Acquired, OwnerToken = entry.OwnerToken });
            }

            if (!_entries.TryGetValue(key, out IdempotencyEntry? existingEntry))
            {
                continue;
            }

            if (existingEntry.Fingerprint != fingerprint)
                return ValueTask.FromResult(new IdempotencyAcquireResult { Status = IdempotencyAcquireStatus.Conflict });

            return ValueTask.FromResult(existingEntry.State switch
            {
                IdempotencyState.InProgress => new IdempotencyAcquireResult { Status = IdempotencyAcquireStatus.InProgress },
                IdempotencyState.Completed => new IdempotencyAcquireResult
                {
                    Status = IdempotencyAcquireStatus.Completed,
                    Payload = existingEntry.Payload,
                },
                _ => throw new InvalidOperationException()
            });
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

        IdempotencyEntry completedEntry = existingEntry with
        {
            State = IdempotencyState.Completed,
            OwnerToken = null,
            Payload = payload
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

        return ValueTask.FromResult(_entries.TryRemove(new KeyValuePair<IdempotencyKey, IdempotencyEntry>(key, existingEntry)));
    }
}