using System.Net;
using System.Net.Sockets;
using Carried.Idempotency.Options;
using Carried.Idempotency.Redis.Options;
using Carried.Idempotency.Redis.Store;
using StackExchange.Redis;

namespace Carried.Idempotency.Redis.Tests;

public sealed class RedisStoreFailureTests : IAsyncLifetime
{
    private ConnectionMultiplexer _redis = null!;
    private RedisStore _store = null!;

    public async Task InitializeAsync()
    {
        int unavailablePort = GetUnusedPort();

        var configuration = new ConfigurationOptions
        {
            EndPoints = { $"127.0.0.1:{unavailablePort}" },
            AbortOnConnectFail = false,
            ConnectTimeout = 250,
            SyncTimeout = 250,
            AsyncTimeout = 250,
            ConnectRetry = 0
        };

        _redis = await ConnectionMultiplexer.ConnectAsync(configuration);

        _store = new RedisStore(
            _redis.GetDatabase(),
            new IdempotencyOptions
            {
                LeaseDuration = TimeSpan.FromSeconds(5),
                CompletedRetention = TimeSpan.FromMinutes(1)
            },
            new RedisIdempotencyOptions
            {
                KeyPrefix = "redis-failure-tests:"
            });
    }

    public async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenRedisIsUnavailable_ThrowsRedisException()
    {
        IdempotencyKey key = CreateKey();

        await Assert.ThrowsAnyAsync<RedisException>(
            () => _store.TryAcquireAsync(
                    key,
                    "fingerprint")
                .AsTask());
    }

    [Fact]
    public async Task TryCompleteAsync_WhenRedisIsUnavailable_ThrowsRedisException()
    {
        IdempotencyKey key = CreateKey();

        await Assert.ThrowsAnyAsync<RedisException>(
            () => _store.TryCompleteAsync(
                    key,
                    Guid.NewGuid(),
                    [1, 2, 3])
                .AsTask());
    }

    [Fact]
    public async Task TryReleaseAsync_WhenRedisIsUnavailable_ThrowsRedisException()
    {
        IdempotencyKey key = CreateKey();

        await Assert.ThrowsAnyAsync<RedisException>(
            () => _store.TryReleaseAsync(
                    key,
                    Guid.NewGuid())
                .AsTask());
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WhenRedisIsUnavailable_ThrowsRedisException()
    {
        IdempotencyKey key = CreateKey();

        await Assert.ThrowsAnyAsync<RedisException>(
            () => _store.TryRenewLeaseAsync(
                    key,
                    Guid.NewGuid())
                .AsTask());
    }

    private static IdempotencyKey CreateKey()
    {
        return new IdempotencyKey(
            "redis-failure-tests",
            Guid.NewGuid().ToString("N"));
    }

    private static int GetUnusedPort()
    {
        using var listener = new TcpListener(
            IPAddress.Loopback,
            0);

        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}