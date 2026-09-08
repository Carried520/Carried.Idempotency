using Carried.Idempotency.Options;
using Carried.Idempotency.Redis.Extensions;
using Carried.Idempotency.Redis.KeyBuilder;
using Carried.Idempotency.Redis.Options;
using Carried.Idempotency.Redis.Store;
using Carried.Idempotency.Store;
using StackExchange.Redis;

namespace Carried.Idempotency.Redis.Tests;

public sealed class RedisStoreHardeningTests : IAsyncLifetime
{
    private ConnectionMultiplexer _redis = null!;
    private IDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        _database = _redis.GetDatabase();
    }

    public async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task DifferentKeyPrefixes_DoNotShareState()
    {
        IdempotencyKey key = CreateKey();

        RedisStore firstStore = CreateStore(
            new RedisIdempotencyOptions
            {
                KeyPrefix = "app-a:idempotency:"
            });

        RedisStore secondStore = CreateStore(
            new RedisIdempotencyOptions
            {
                KeyPrefix = "app-b:idempotency:"
            });

        IdempotencyAcquireResult first =
            await firstStore.TryAcquireAsync(key, "fingerprint");

        IdempotencyAcquireResult second =
            await secondStore.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, first.Status);
        Assert.Equal(IdempotencyAcquireStatus.Acquired, second.Status);
        Assert.NotEqual(first.OwnerToken, second.OwnerToken);
    }

    [Fact]
    public void RedisKeyMapper_DifferentBoundaries_ProduceDifferentKeys()
    {
        const string prefix = "test:idempotency:";

        IdempotencyKey first = new("ab", "c");
        IdempotencyKey second = new("a", "bc");

        RedisKey firstKey = RedisKeyMapper.From(first, prefix);
        RedisKey secondKey = RedisKeyMapper.From(second, prefix);

        Assert.NotEqual(firstKey, secondKey);
    }

    [Fact]
    public void RedisKeyMapper_SameInput_ProducesSameKey()
    {
        const string prefix = "test:idempotency:";

        IdempotencyKey key = new(
            "orders:żółć:日本語",
            "ключ:🚀:123");

        RedisKey first = RedisKeyMapper.From(key, prefix);
        RedisKey second = RedisKeyMapper.From(key, prefix);

        Assert.Equal(first, second);
    }

    [Fact]
    public void RedisKeyMapper_DifferentPrefixes_ProduceDifferentKeys()
    {
        IdempotencyKey key = CreateKey();

        RedisKey first =
            RedisKeyMapper.From(key, "app-a:idempotency:");

        RedisKey second =
            RedisKeyMapper.From(key, "app-b:idempotency:");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task TryAcquireAsync_WithPreCancelledToken_DoesNotAcquire()
    {
        RedisStore store = CreateStore();
        IdempotencyKey key = CreateKey();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store
                .TryAcquireAsync(
                    key,
                    "fingerprint",
                    cancellationTokenSource.Token)
                .AsTask());

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
    }

    [Fact]
    public async Task TryCompleteAsync_WithPreCancelledToken_DoesNotComplete()
    {
        RedisStore store = CreateStore();
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store
                .TryCompleteAsync(
                    key,
                    acquired.OwnerToken!.Value,
                    [1, 2, 3],
                    cancellationTokenSource.Token)
                .AsTask());

        bool completed = await store.TryCompleteAsync(
            key,
            acquired.OwnerToken.Value,
            [4, 5, 6]);

        Assert.True(completed);

        IdempotencyAcquireResult replay =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Completed, replay.Status);
        Assert.Equal([4, 5, 6], replay.Payload);
    }

    [Fact]
    public async Task TryReleaseAsync_WithPreCancelledToken_DoesNotRelease()
    {
        RedisStore store = CreateStore();
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store
                .TryReleaseAsync(
                    key,
                    acquired.OwnerToken!.Value,
                    cancellationTokenSource.Token)
                .AsTask());

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WithPreCancelledToken_DoesNotThrowAfterwards()
    {
        RedisStore store = CreateStore();
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store
                .TryRenewLeaseAsync(
                    key,
                    acquired.OwnerToken!.Value,
                    cancellationTokenSource.Token)
                .AsTask());

        bool renewed = await store.TryRenewLeaseAsync(
            key,
            acquired.OwnerToken.Value);

        Assert.True(renewed);
    }

    [Fact]
    public async Task CompletedPayload_CanBeEmpty()
    {
        RedisStore store = CreateStore();
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        bool completed = await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            []);

        Assert.True(completed);

        IdempotencyAcquireResult replay =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Completed, replay.Status);
        Assert.NotNull(replay.Payload);
        Assert.Empty(replay.Payload);
    }

    [Fact]
    public async Task CompletedPayload_PreservesArbitraryBinaryData()
    {
        RedisStore store = CreateStore();
        IdempotencyKey key = CreateKey();

        byte[] payload =
        [
            0x00,
            0xFF,
            0x80,
            0x7F,
            0x01,
            0x00,
            0xFE,
            0x42
        ];

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        bool completed = await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            payload);

        Assert.True(completed);

        IdempotencyAcquireResult replay =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Completed, replay.Status);
        Assert.Equal(payload, replay.Payload);
    }

    [Fact]
    public async Task CompletedPayload_PreservesLargePayload()
    {
        RedisStore store = CreateStore();
        IdempotencyKey key = CreateKey();

        byte[] payload = new byte[1024 * 1024];
        Random.Shared.NextBytes(payload);

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        bool completed = await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            payload);

        Assert.True(completed);

        IdempotencyAcquireResult replay =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Completed, replay.Status);
        Assert.Equal(payload, replay.Payload);
    }

    [Fact]
    public async Task UnicodeFingerprint_IsPreservedForConflictDetection()
    {
        RedisStore store = CreateStore();
        IdempotencyKey key = CreateKey();

        const string fingerprint = "żółć-日本語-🚀-ключ";

        IdempotencyAcquireResult first =
            await store.TryAcquireAsync(key, fingerprint);

        IdempotencyAcquireResult same =
            await store.TryAcquireAsync(key, fingerprint);

        IdempotencyAcquireResult different =
            await store.TryAcquireAsync(
                key,
                fingerprint + "-different");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, first.Status);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, same.Status);
        Assert.Equal(IdempotencyAcquireStatus.Conflict, different.Status);
    }

    [Fact]
    public void CreateRedis_WhenKeyPrefixIsWhitespace_Throws()
    {
        var options = new IdempotencyOptions();

        var redisOptions = new RedisIdempotencyOptions
        {
            KeyPrefix = "   "
        };

        Assert.Throws<ArgumentException>(() =>
            IdempotencyService.CreateRedis(
                _database,
                options,
                redisOptions));
    }

    [Fact]
    public void CreateRedis_WhenLeaseDurationIsLessThanOneMillisecond_Throws()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromTicks(
                TimeSpan.TicksPerMillisecond - 1)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IdempotencyService.CreateRedis(
                _database,
                options));
    }

    [Fact]
    public void CreateRedis_WhenCompletedRetentionIsLessThanOneMillisecond_Throws()
    {
        var options = new IdempotencyOptions
        {
            CompletedRetention = TimeSpan.FromTicks(
                TimeSpan.TicksPerMillisecond - 1)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IdempotencyService.CreateRedis(
                _database,
                options));
    }

    [Fact]
    public async Task StaleOwner_CompetingWithReacquisition_CannotComplete()
    {
        RedisStore store = CreateStore(
            leaseDuration: TimeSpan.FromMilliseconds(500));

        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult first =
            await store.TryAcquireAsync(key, "fingerprint");

        await Task.Delay(TimeSpan.FromMilliseconds(800));

        Task<IdempotencyAcquireResult> acquireTask =
            store.TryAcquireAsync(key, "fingerprint").AsTask();

        Task<bool> staleCompleteTask =
            store.TryCompleteAsync(
                    key,
                    first.OwnerToken!.Value,
                    [1, 2, 3])
                .AsTask();

        await Task.WhenAll(acquireTask, staleCompleteTask);

        IdempotencyAcquireResult second = await acquireTask;
        bool staleCompleted = await staleCompleteTask;

        Assert.Equal(IdempotencyAcquireStatus.Acquired, second.Status);
        Assert.NotEqual(first.OwnerToken, second.OwnerToken);
        Assert.False(staleCompleted);
    }

    private RedisStore CreateStore(
        RedisIdempotencyOptions? redisOptions = null,
        TimeSpan? leaseDuration = null)
    {
        return new RedisStore(
            _database,
            new IdempotencyOptions
            {
                LeaseDuration =
                    leaseDuration ?? TimeSpan.FromSeconds(2),

                CompletedRetention =
                    TimeSpan.FromSeconds(5)
            },
            redisOptions ?? new RedisIdempotencyOptions());
    }

    private static IdempotencyKey CreateKey()
    {
        return new IdempotencyKey(
            "redis-hardening-tests",
            Guid.NewGuid().ToString("N"));
    }
}