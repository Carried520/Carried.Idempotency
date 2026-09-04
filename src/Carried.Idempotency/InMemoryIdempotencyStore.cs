using System.Collections.Concurrent;

namespace Carried.Idempotency;

internal class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<IdempotencyKey, IdempotencyEntry> _entries = new();

    public IdempotencyAcquireResult TryAcquire(IdempotencyKey key, string fingerprint)
    {
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
                return new IdempotencyAcquireResult { Status = IdempotencyAcquireStatus.Acquired, OwnerToken = entry.OwnerToken };
            }

            if (!_entries.TryGetValue(key, out IdempotencyEntry? existingEntry))
            {
                continue;
            }

            if (existingEntry.Fingerprint != fingerprint)
                return new IdempotencyAcquireResult { Status = IdempotencyAcquireStatus.Conflict };

            return existingEntry.State switch
            {
                IdempotencyState.InProgress => new IdempotencyAcquireResult { Status = IdempotencyAcquireStatus.InProgress },
                IdempotencyState.Completed => new IdempotencyAcquireResult
                {
                    Status = IdempotencyAcquireStatus.Completed,
                    Payload = existingEntry.Payload,
                },
                _ => throw new InvalidOperationException()
            };
        }
    }

    public bool TryComplete(IdempotencyKey key, Guid ownerToken, byte[] payload)
    {
        if (!_entries.TryGetValue(key, out IdempotencyEntry? existingEntry))
        {
            return false;
        }

        if (existingEntry.State is not IdempotencyState.InProgress || existingEntry.OwnerToken != ownerToken)
            return false;

        IdempotencyEntry completedEntry = existingEntry with
        {
            State = IdempotencyState.Completed,
            OwnerToken = null,
            Payload = payload
        };

        return _entries.TryUpdate(key, completedEntry, existingEntry);
    }

    public bool TryRelease(IdempotencyKey key, Guid ownerToken)
    {
        if (!_entries.TryGetValue(key, out IdempotencyEntry? existingEntry))
        {
            return false;
        }

        if (existingEntry.State is not IdempotencyState.InProgress ||
            existingEntry.OwnerToken != ownerToken)
            return false;

        return _entries.TryRemove(new KeyValuePair<IdempotencyKey, IdempotencyEntry>(key, existingEntry));
    }
}