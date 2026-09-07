using Carried.Idempotency.Options;
using Carried.Idempotency.Redis.KeyBuilder;
using Carried.Idempotency.Redis.Scripts;
using Carried.Idempotency.Store;
using StackExchange.Redis;

namespace Carried.Idempotency.Redis.Store;

internal sealed class RedisStore : IIdempotencyStore
{
    private readonly IDatabase _database;
    private readonly IdempotencyOptions _options;

    public RedisStore(IDatabase database, IdempotencyOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _database = database;
        _options = options;
    }

    public async ValueTask<IdempotencyAcquireResult> TryAcquireAsync(IdempotencyKey key,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        RedisKey redisKey = RedisKeyMapper.From(key);
        var ownerToken = Guid.NewGuid();

        var acquireResult = (RedisResult[])(await _database.ScriptEvaluateAsync(
            RedisScripts.Acquire,
            [redisKey],
            [fingerprint, ownerToken.ToString("N"), (long)_options.LeaseDuration.TotalMilliseconds]))!;

        return MapAcquireResult(acquireResult, ownerToken);
    }

    public async ValueTask<bool> TryCompleteAsync(IdempotencyKey key,
        Guid ownerToken,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        RedisKey redisKey = RedisKeyMapper.From(key);

        RedisResult completeResult = (await _database.ScriptEvaluateAsync(
            RedisScripts.Complete,
            [redisKey],
            [ownerToken.ToString("N"), payload, (long)_options.CompletedRetention.TotalMilliseconds]));

        return (int)completeResult switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidOperationException("Complete result was out of range.")
        };
    }

    public async ValueTask<bool> TryReleaseAsync(IdempotencyKey key,
        Guid ownerToken,
        CancellationToken cancellationToken = default)
    {
        RedisKey redisKey = RedisKeyMapper.From(key);

        RedisResult releaseResult = await _database.ScriptEvaluateAsync(
            RedisScripts.Release,
            [redisKey],
            [ownerToken.ToString("N")]);

        return (int)releaseResult switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidOperationException("Release result was out of range.")
        };
    }

    public async ValueTask<bool> TryRenewLeaseAsync(IdempotencyKey key,
        Guid ownerToken,
        CancellationToken cancellationToken = default)
    {
        RedisKey redisKey = RedisKeyMapper.From(key);

        RedisResult renewLeaseResult = await _database.ScriptEvaluateAsync(
            RedisScripts.RenewLease,
            [redisKey],
            [ownerToken.ToString("N"), (long)_options.LeaseDuration.TotalMilliseconds]);

        return (int)renewLeaseResult switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidOperationException("Renew lease result was out of range.")
        };
    }


    private static IdempotencyAcquireResult MapAcquireResult(
        RedisResult[] result,
        Guid ownerToken)
    {
        if (result.Length == 0)
            throw new InvalidOperationException("Redis acquire script returned an empty result.");

        var status = (RedisAcquireStatus)(int)result[0];

        return status switch
        {
            RedisAcquireStatus.Acquired =>
                IdempotencyAcquireResult.Acquired(ownerToken),

            RedisAcquireStatus.InProgress =>
                IdempotencyAcquireResult.InProgress(),

            RedisAcquireStatus.Completed when result.Length == 2 =>
                IdempotencyAcquireResult.Completed((byte[])result[1]!),

            RedisAcquireStatus.Completed =>
                throw new InvalidOperationException("Redis acquire script returned an invalid completed result."),

            RedisAcquireStatus.Conflict =>
                IdempotencyAcquireResult.Conflict(),

            _ =>
                throw new InvalidOperationException($"Redis acquire script returned unknown status '{(int)status}'.")
        };
    }
}