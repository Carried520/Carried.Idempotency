using Carried.Idempotency.Options;
using Carried.Idempotency.Redis.Options;
using Carried.Idempotency.Redis.Store;
using Carried.Idempotency.Store;
using StackExchange.Redis;

namespace Carried.Idempotency.Redis.Tests;

public sealed class RedisStoreTests : IAsyncLifetime
{
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
                LeaseDuration = TimeSpan.FromSeconds(2),
                CompletedRetention = TimeSpan.FromSeconds(5)
            },
            new RedisIdempotencyOptions());
    }

    public async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenKeyDoesNotExist_ReturnsAcquired()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult result =
            await _store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
        Assert.NotNull(result.OwnerToken);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenSameFingerprintIsAlreadyInProgress_ReturnsInProgress()
    {
        IdempotencyKey key = CreateKey();

        await _store.TryAcquireAsync(key, "fingerprint");

        IdempotencyAcquireResult result =
            await _store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenFingerprintDiffers_ReturnsConflict()
    {
        IdempotencyKey key = CreateKey();

        await _store.TryAcquireAsync(key, "fingerprint-a");

        IdempotencyAcquireResult result =
            await _store.TryAcquireAsync(key, "fingerprint-b");

        Assert.Equal(IdempotencyAcquireStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task TryAcquireAsync_AfterCompletion_ReturnsCompletedPayload()
    {
        IdempotencyKey key = CreateKey();
        byte[] payload = [1, 2, 3, 4];

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        bool completed = await _store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            payload);

        Assert.True(completed);

        IdempotencyAcquireResult replay =
            await _store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Completed, replay.Status);
        Assert.Equal(payload, replay.Payload);
    }

    [Fact]
    public async Task TryAcquireAsync_AfterCompletionWithDifferentFingerprint_ReturnsConflict()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint-a");

        bool completed = await _store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.True(completed);

        IdempotencyAcquireResult result =
            await _store.TryAcquireAsync(key, "fingerprint-b");

        Assert.Equal(IdempotencyAcquireStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task TryCompleteAsync_WithCorrectOwner_ReturnsTrue()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        bool result = await _store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.True(result);
    }

    [Fact]
    public async Task TryCompleteAsync_WithWrongOwner_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();

        await _store.TryAcquireAsync(key, "fingerprint");

        bool result = await _store.TryCompleteAsync(
            key,
            Guid.NewGuid(),
            [1, 2, 3]);

        Assert.False(result);
    }

    [Fact]
    public async Task TryReleaseAsync_WithCorrectOwner_ReturnsTrue()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        bool released = await _store.TryReleaseAsync(
            key,
            acquired.OwnerToken!.Value);

        Assert.True(released);
    }

    [Fact]
    public async Task TryReleaseAsync_WithWrongOwner_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();

        await _store.TryAcquireAsync(key, "fingerprint");

        bool released = await _store.TryReleaseAsync(
            key,
            Guid.NewGuid());

        Assert.False(released);
    }

    [Fact]
    public async Task TryReleaseAsync_AfterRelease_AllowsReacquisition()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        bool released = await _store.TryReleaseAsync(
            key,
            acquired.OwnerToken!.Value);

        Assert.True(released);

        IdempotencyAcquireResult reacquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, reacquired.Status);
        Assert.NotEqual(acquired.OwnerToken, reacquired.OwnerToken);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WithCorrectOwner_ReturnsTrue()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        bool renewed = await _store.TryRenewLeaseAsync(
            key,
            acquired.OwnerToken!.Value);

        Assert.True(renewed);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WithWrongOwner_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();

        await _store.TryAcquireAsync(key, "fingerprint");

        bool renewed = await _store.TryRenewLeaseAsync(
            key,
            Guid.NewGuid());

        Assert.False(renewed);
    }

    [Fact]
    public async Task ConcurrentAcquire_AllowsExactlyOneOwner()
    {
        IdempotencyKey key = CreateKey();

        Task<IdempotencyAcquireResult>[] tasks = Enumerable
            .Range(0, 20)
            .Select(_ =>
                _store.TryAcquireAsync(key, "fingerprint").AsTask())
            .ToArray();

        IdempotencyAcquireResult[] results =
            await Task.WhenAll(tasks);

        Assert.Single(results, result => result.Status == IdempotencyAcquireStatus.Acquired);

        Assert.Equal(
            19,
            results.Count(result => result.Status == IdempotencyAcquireStatus.InProgress));
    }

    [Fact]
    public async Task TryCompleteAsync_WhenAlreadyCompleted_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        bool completed = await _store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.True(completed);

        bool result = await _store.TryCompleteAsync(
            key,
            acquired.OwnerToken.Value,
            [4, 5, 6]);

        Assert.False(result);
    }

    [Fact]
    public async Task TryReleaseAsync_WhenAlreadyCompleted_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        bool completed = await _store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.True(completed);

        bool result = await _store.TryReleaseAsync(
            key,
            acquired.OwnerToken.Value);

        Assert.False(result);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WhenAlreadyCompleted_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await _store.TryAcquireAsync(key, "fingerprint");

        bool completed = await _store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.True(completed);

        bool result = await _store.TryRenewLeaseAsync(
            key,
            acquired.OwnerToken.Value);

        Assert.False(result);
    }

    private static IdempotencyKey CreateKey()
    {
        return new IdempotencyKey(
            "redis-tests",
            Guid.NewGuid().ToString("N"));
    }
}