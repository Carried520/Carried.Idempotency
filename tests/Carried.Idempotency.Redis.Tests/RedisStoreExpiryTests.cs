using Carried.Idempotency.Options;
using Carried.Idempotency.Redis.Options;
using Carried.Idempotency.Redis.Store;
using Carried.Idempotency.Store;
using StackExchange.Redis;

namespace Carried.Idempotency.Redis.Tests;

public sealed class RedisStoreExpiryTests : IAsyncLifetime
{
    private static readonly TimeSpan LeaseDuration =
        TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan CompletedRetention =
        TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan ExpiryWait =
        TimeSpan.FromMilliseconds(800);

    private ConnectionMultiplexer _redis = null!;
    private RedisStore _store = null!;

    public async Task InitializeAsync()
    {
        _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        IDatabase database = _redis.GetDatabase();

        _store = new RedisStore(
            database,
            new IdempotencyOptions
            {
                LeaseDuration = LeaseDuration,
                CompletedRetention = CompletedRetention
            },
            new RedisIdempotencyOptions());
    }

    public async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenInProgressLeaseExpires_ReturnsAcquired()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult first =
            await _store.TryAcquireAsync(key, "fingerprint");

        await Task.Delay(ExpiryWait);

        IdempotencyAcquireResult second =
            await _store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, second.Status);
        Assert.NotEqual(first.OwnerToken, second.OwnerToken);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenExpiredEntryHasDifferentFingerprint_ReturnsAcquired()
    {
        IdempotencyKey key = CreateKey();

        await _store.TryAcquireAsync(key, "fingerprint-a");

        await Task.Delay(ExpiryWait);

        IdempotencyAcquireResult result =
            await _store.TryAcquireAsync(key, "fingerprint-b");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
        Assert.NotNull(result.OwnerToken);
    }

    [Fact]
    public async Task TryCompleteAsync_WhenLeaseExpired_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        await Task.Delay(ExpiryWait);

        bool result = await _store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.False(result);
    }

    [Fact]
    public async Task TryReleaseAsync_WhenLeaseExpired_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        await Task.Delay(ExpiryWait);

        bool result = await _store.TryReleaseAsync(
            key,
            acquired.OwnerToken!.Value);

        Assert.False(result);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WhenLeaseExpired_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        await Task.Delay(ExpiryWait);

        bool result = await _store.TryRenewLeaseAsync(
            key,
            acquired.OwnerToken!.Value);

        Assert.False(result);
    }

    [Fact]
    public async Task StaleOwner_AfterReacquisition_CannotComplete()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult first =
            await _store.TryAcquireAsync(key, "fingerprint");

        await Task.Delay(ExpiryWait);

        IdempotencyAcquireResult second =
            await _store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, second.Status);

        bool result = await _store.TryCompleteAsync(
            key,
            first.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.False(result);
    }

    [Fact]
    public async Task StaleOwner_AfterReacquisition_CannotRelease()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult first =
            await _store.TryAcquireAsync(key, "fingerprint");

        await Task.Delay(ExpiryWait);

        IdempotencyAcquireResult second =
            await _store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, second.Status);

        bool result = await _store.TryReleaseAsync(
            key,
            first.OwnerToken!.Value);

        Assert.False(result);
    }

    [Fact]
    public async Task StaleOwner_AfterReacquisition_CannotRenewLease()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult first =
            await _store.TryAcquireAsync(key, "fingerprint");

        await Task.Delay(ExpiryWait);

        IdempotencyAcquireResult second =
            await _store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, second.Status);

        bool result = await _store.TryRenewLeaseAsync(
            key,
            first.OwnerToken!.Value);

        Assert.False(result);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenCompletedRetentionExpires_ReturnsAcquired()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult first =
            await _store.TryAcquireAsync(key, "fingerprint");

        bool completed = await _store.TryCompleteAsync(
            key,
            first.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.True(completed);

        await Task.Delay(ExpiryWait);

        IdempotencyAcquireResult result =
            await _store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
        Assert.NotEqual(first.OwnerToken, result.OwnerToken);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_ExtendsLease()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        bool renewed = await _store.TryRenewLeaseAsync(
            key,
            acquired.OwnerToken!.Value);

        Assert.True(renewed);

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        IdempotencyAcquireResult result =
            await _store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.InProgress, result.Status);
    }

    private static IdempotencyKey CreateKey()
    {
        return new IdempotencyKey(
            "redis-expiry-tests",
            Guid.NewGuid().ToString("N"));
    }
}