using Carried.Idempotency.Options;
using Carried.Idempotency.Store;

namespace Carried.Idempotency.Tests;

public partial class InMemoryIdempotencyStoreTests
{
    [Fact]
    public async Task TryAcquire_FirstAttempt_ReturnsAcquired()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
        Assert.NotNull(result.OwnerToken);
    }

    [Fact]
    public async Task TryAcquire_SameKeyAndFingerprint_ReturnsInProgress()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        await store.TryAcquireAsync(key, "fingerprint");

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task TryAcquire_SameKeyDifferentFingerprint_ReturnsConflict()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        await store.TryAcquireAsync(key, "fingerprint-a");

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint-b");

        Assert.Equal(IdempotencyAcquireStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task TryComplete_WithCorrectOwner_CompletesEntry()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        byte[] payload = [1, 2, 3];

        bool completed = await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            payload);

        IdempotencyAcquireResult replay =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.True(completed);
        Assert.Equal(IdempotencyAcquireStatus.Completed, replay.Status);
        Assert.Equal(payload, replay.Payload);
    }

    [Fact]
    public async Task TryComplete_WithWrongOwner_ReturnsFalse()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        await store.TryAcquireAsync(key, "fingerprint");

        bool completed = await store.TryCompleteAsync(
            key,
            Guid.NewGuid(),
            [1, 2, 3]);

        Assert.False(completed);
    }

    [Fact]
    public async Task TryRelease_WithCorrectOwner_AllowsAcquireAgain()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        bool released = await store.TryReleaseAsync(
            key,
            acquired.OwnerToken!.Value);

        IdempotencyAcquireResult secondAcquire =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.True(released);
        Assert.Equal(
            IdempotencyAcquireStatus.Acquired,
            secondAcquire.Status);
    }

    [Fact]
    public async Task TryRelease_WithWrongOwner_ReturnsFalse()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        await store.TryAcquireAsync(key, "fingerprint");

        bool released = await store.TryReleaseAsync(
            key,
            Guid.NewGuid());

        Assert.False(released);

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task TryAcquire_ConcurrentCalls_OnlyOneAcquires()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        Task<IdempotencyAcquireResult>[] tasks = Enumerable
            .Range(0, 100)
            .AsParallel()
            .Select(_ =>
                store.TryAcquireAsync(
                    key,
                    "fingerprint").AsTask())
            .ToArray();

        IdempotencyAcquireResult[] results =
            await Task.WhenAll(tasks);

        Assert.Single(
            results,
            x => x.Status == IdempotencyAcquireStatus.Acquired);

        Assert.Equal(
            99,
            results.Count(
                x => x.Status == IdempotencyAcquireStatus.InProgress));
    }
}