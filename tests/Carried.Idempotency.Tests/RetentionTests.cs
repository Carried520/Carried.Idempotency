using Carried.Idempotency.Options;
using Carried.Idempotency.Store;
using Microsoft.Extensions.Time.Testing;

namespace Carried.Idempotency.Tests;

public partial class InMemoryIdempotencyStoreTests
{
    [Fact]
    public async Task TryAcquire_CompletedEntryBeforeRetentionExpiry_ReturnsCompleted()
    {
        var options = new IdempotencyOptions
        {
            CompletedRetention = TimeSpan.FromHours(1)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        byte[] payload = [1, 2, 3];

        await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            payload);

        timeProvider.Advance(TimeSpan.FromMinutes(59));

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Completed, result.Status);
        Assert.Equal(payload, result.Payload);
    }

    [Fact]
    public async Task TryAcquire_CompletedEntryAtRetentionExpiry_ReturnsAcquired()
    {
        var options = new IdempotencyOptions
        {
            CompletedRetention = TimeSpan.FromHours(1)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult first =
            await store.TryAcquireAsync(key, "fingerprint");

        await store.TryCompleteAsync(
            key,
            first.OwnerToken!.Value,
            [1, 2, 3]);

        timeProvider.Advance(TimeSpan.FromHours(1));

        IdempotencyAcquireResult second =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, second.Status);
        Assert.NotNull(second.OwnerToken);
        Assert.NotEqual(first.OwnerToken, second.OwnerToken);
    }

    [Fact]
    public async Task TryAcquire_CompletedEntryBeforeRetentionExpiry_WithDifferentFingerprint_ReturnsConflict()
    {
        var options = new IdempotencyOptions
        {
            CompletedRetention = TimeSpan.FromHours(1)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint-a");

        await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        timeProvider.Advance(TimeSpan.FromMinutes(59));

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint-b");

        Assert.Equal(IdempotencyAcquireStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task TryAcquire_ExpiredCompletedEntry_WithDifferentFingerprint_ReturnsAcquired()
    {
        var options = new IdempotencyOptions
        {
            CompletedRetention = TimeSpan.FromHours(1)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult first =
            await store.TryAcquireAsync(key, "fingerprint-a");

        await store.TryCompleteAsync(
            key,
            first.OwnerToken!.Value,
            [1, 2, 3]);

        timeProvider.Advance(TimeSpan.FromHours(1));

        IdempotencyAcquireResult second =
            await store.TryAcquireAsync(key, "fingerprint-b");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, second.Status);
        Assert.NotNull(second.OwnerToken);
        Assert.NotEqual(first.OwnerToken, second.OwnerToken);
    }

    [Fact]
    public async Task TryAcquire_ConcurrentCallsAfterRetentionExpiry_OnlyOneAcquires()
    {
        var options = new IdempotencyOptions
        {
            CompletedRetention = TimeSpan.FromHours(1)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        timeProvider.Advance(TimeSpan.FromHours(1));

        Task<IdempotencyAcquireResult>[] tasks = Enumerable
            .Range(0, 100)
            .Select(_ =>
                store.TryAcquireAsync(
                    key,
                    "fingerprint").AsTask())
            .ToArray();

        IdempotencyAcquireResult[] results =
            await Task.WhenAll(tasks);

        Assert.Single(
            results,
            result => result.Status == IdempotencyAcquireStatus.Acquired);

        Assert.Equal(
            99,
            results.Count(result => result.Status == IdempotencyAcquireStatus.InProgress));
    }

    [Fact]
    public async Task TryComplete_ReacquiredExpiredEntry_StartsNewRetentionWindow()
    {
        var options = new IdempotencyOptions
        {
            CompletedRetention = TimeSpan.FromHours(1)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult first =
            await store.TryAcquireAsync(key, "fingerprint");

        await store.TryCompleteAsync(
            key,
            first.OwnerToken!.Value,
            [1, 2, 3]);
        
        timeProvider.Advance(TimeSpan.FromHours(1));

        IdempotencyAcquireResult second =
            await store.TryAcquireAsync(key, "fingerprint");

        byte[] newPayload = [4, 5, 6];

        bool completed = await store.TryCompleteAsync(
            key,
            second.OwnerToken!.Value,
            newPayload);
        
        timeProvider.Advance(TimeSpan.FromMinutes(59));

        IdempotencyAcquireResult replay =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.True(completed);
        Assert.Equal(IdempotencyAcquireStatus.Completed, replay.Status);
        Assert.Equal(newPayload, replay.Payload);
    }
}