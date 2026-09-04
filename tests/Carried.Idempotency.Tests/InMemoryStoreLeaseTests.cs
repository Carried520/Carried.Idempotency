using Carried.Idempotency.Options;
using Carried.Idempotency.Store;
using Microsoft.Extensions.Time.Testing;

namespace Carried.Idempotency.Tests;

public partial class InMemoryIdempotencyStoreTests
{
    [Fact]
    public async Task TryAcquire_ActiveLease_ReturnsInProgress()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(5)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        await store.TryAcquireAsync(key, "fingerprint");

        timeProvider.Advance(TimeSpan.FromMinutes(4));

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(
            IdempotencyAcquireStatus.InProgress,
            result.Status);
    }

    [Fact]
    public async Task TryAcquire_ExpiredLease_AcquiresWithNewOwner()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(5)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult first =
            await store.TryAcquireAsync(key, "fingerprint");

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        IdempotencyAcquireResult second =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(
            IdempotencyAcquireStatus.Acquired,
            second.Status);

        Assert.NotNull(first.OwnerToken);
        Assert.NotNull(second.OwnerToken);
        Assert.NotEqual(first.OwnerToken, second.OwnerToken);
    }

    [Fact]
    public async Task TryComplete_AfterLeaseTakeover_WithOldOwner_ReturnsFalse()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(5)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult first =
            await store.TryAcquireAsync(key, "fingerprint");

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await store.TryAcquireAsync(key, "fingerprint");

        bool completed = await store.TryCompleteAsync(
            key,
            first.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.False(completed);
    }

    [Fact]
    public async Task TryRelease_AfterLeaseTakeover_WithOldOwner_ReturnsFalse()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(5)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult first =
            await store.TryAcquireAsync(key, "fingerprint");

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await store.TryAcquireAsync(key, "fingerprint");

        bool released = await store.TryReleaseAsync(
            key,
            first.OwnerToken!.Value);

        Assert.False(released);

        IdempotencyAcquireResult current =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(
            IdempotencyAcquireStatus.InProgress,
            current.Status);
    }

    [Fact]
    public async Task TryRenewLease_WithValidOwner_ReturnsTrue()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(5)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        timeProvider.Advance(TimeSpan.FromMinutes(4));

        bool renewed = await store.TryRenewLeaseAsync(
            key,
            acquired.OwnerToken!.Value);

        Assert.True(renewed);
    }

    [Fact]
    public async Task TryRenewLease_AfterLeaseExpired_ReturnsFalse()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(5)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        bool renewed = await store.TryRenewLeaseAsync(
            key,
            acquired.OwnerToken!.Value);

        Assert.False(renewed);
    }

    [Fact]
    public async Task TryRenewLease_ExtendsLeaseBeyondOriginalExpiration()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(5)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        timeProvider.Advance(TimeSpan.FromMinutes(4));

        bool renewed = await store.TryRenewLeaseAsync(
            key,
            acquired.OwnerToken!.Value);

        timeProvider.Advance(TimeSpan.FromMinutes(2));

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.True(renewed);
        Assert.Equal(
            IdempotencyAcquireStatus.InProgress,
            result.Status);
    }

    [Fact]
    public async Task TryRenewLease_AfterTakeover_WithOldOwner_ReturnsFalse()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(5)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        IdempotencyAcquireResult first =
            await store.TryAcquireAsync(key, "fingerprint");

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await store.TryAcquireAsync(key, "fingerprint");

        bool renewed = await store.TryRenewLeaseAsync(
            key,
            first.OwnerToken!.Value);

        Assert.False(renewed);
    }

    [Fact]
    public async Task TryAcquire_ConcurrentTakeover_OnlyOneAcquires()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(5)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var key = new IdempotencyKey("orders", "123");

        await store.TryAcquireAsync(key, "fingerprint");

        timeProvider.Advance(TimeSpan.FromMinutes(5));

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
}