namespace Carried.Idempotency.Tests;

using Idempotency;

public class InMemoryIdempotencyStoreTests
{
    [Fact]
    public void TryAcquire_FirstAttempt_ReturnsAcquired()
    {
        var store = new InMemoryIdempotencyStore();
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult result = store.TryAcquire(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
        Assert.NotNull(result.OwnerToken);
    }

    [Fact]
    public void TryAcquire_SameKeyAndFingerprint_ReturnsInProgress()
    {
        var store = new InMemoryIdempotencyStore();
        var key = new IdempotencyKey("orders", "123");

        store.TryAcquire(key, "fingerprint");

        IdempotencyAcquireResult result = store.TryAcquire(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.InProgress, result.Status);
    }

    [Fact]
    public void TryAcquire_SameKeyDifferentFingerprint_ReturnsConflict()
    {
        var store = new InMemoryIdempotencyStore();
        var key = new IdempotencyKey("orders", "123");

        store.TryAcquire(key, "fingerprint-a");

        IdempotencyAcquireResult result = store.TryAcquire(key, "fingerprint-b");

        Assert.Equal(IdempotencyAcquireStatus.Conflict, result.Status);
    }
    
    
    [Fact]
    public void TryComplete_WithCorrectOwner_CompletesEntry()
    {
        var store = new InMemoryIdempotencyStore();
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult acquired = store.TryAcquire(key, "fingerprint");

        byte[] payload = [1, 2, 3];

        bool completed = store.TryComplete(
            key,
            acquired.OwnerToken!.Value,
            payload);

        IdempotencyAcquireResult replay = store.TryAcquire(key, "fingerprint");

        Assert.True(completed);
        Assert.Equal(IdempotencyAcquireStatus.Completed, replay.Status);
        Assert.Equal(payload, replay.Payload);
    }

    [Fact]
    public void TryComplete_WithWrongOwner_ReturnsFalse()
    {
        var store = new InMemoryIdempotencyStore();
        var key = new IdempotencyKey("orders", "123");

        store.TryAcquire(key, "fingerprint");

        bool completed = store.TryComplete(
            key,
            Guid.NewGuid(),
            [1, 2, 3]);

        Assert.False(completed);
    }

    [Fact]
    public void TryRelease_WithCorrectOwner_AllowsAcquireAgain()
    {
        var store = new InMemoryIdempotencyStore();
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult acquired = store.TryAcquire(key, "fingerprint");

        bool released = store.TryRelease(
            key,
            acquired.OwnerToken!.Value);

        IdempotencyAcquireResult secondAcquire = store.TryAcquire(
            key,
            "fingerprint");

        Assert.True(released);
        Assert.Equal(IdempotencyAcquireStatus.Acquired, secondAcquire.Status);
    }

    [Fact]
    public void TryRelease_WithWrongOwner_ReturnsFalse()
    {
        var store = new InMemoryIdempotencyStore();
        var key = new IdempotencyKey("orders", "123");

        store.TryAcquire(key, "fingerprint");

        bool released = store.TryRelease(
            key,
            Guid.NewGuid());

        Assert.False(released);

        IdempotencyAcquireResult result = store.TryAcquire(
            key,
            "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.InProgress, result.Status);
    }
    
    [Fact]
    public void TryAcquire_ConcurrentCalls_OnlyOneAcquires()
    {
        var store = new InMemoryIdempotencyStore();
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult[] results = Enumerable
            .Range(0, 100)
            .AsParallel()
            .Select(_ => store.TryAcquire(key, "fingerprint"))
            .ToArray();

        Assert.Single(results, x => x.Status == IdempotencyAcquireStatus.Acquired);

        Assert.Equal(
            99,
            results.Count(x => x.Status == IdempotencyAcquireStatus.InProgress));
    }
}