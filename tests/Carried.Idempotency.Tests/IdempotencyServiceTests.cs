using Carried.Idempotency.Exceptions;
using Carried.Idempotency.Options;
using Carried.Idempotency.Serialization;
using Carried.Idempotency.Store;

namespace Carried.Idempotency.Tests;

public partial class IdempotencyServiceTests
{
    [Fact]
    public async Task ExecuteAsync_FirstExecution_RunsOperationAndReturnsResult()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var serializer = new JsonIdempotencySerializer();
        var service = new IdempotencyService(
            store,
            serializer,
            options,
            timeProvider);

        var key = new IdempotencyKey("orders", "123");

        int executionCount = 0;

        string? result = await service.ExecuteAsync(
            key,
            "fingerprint",
            _ =>
            {
                executionCount++;
                return Task.FromResult<string?>("result");
            });

        Assert.Equal("result", result);
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task ExecuteAsync_CompletedOperation_ReplaysResultWithoutExecutingAgain()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var serializer = new JsonIdempotencySerializer();
        var service = new IdempotencyService(
            store,
            serializer,
            options,
            timeProvider);

        var key = new IdempotencyKey("orders", "123");

        int executionCount = 0;

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ =>
            {
                executionCount++;
                return Task.FromResult<string?>("result");
            });

        string? replayed = await service.ExecuteAsync(
            key,
            "fingerprint",
            _ =>
            {
                executionCount++;
                return Task.FromResult<string?>("different-result");
            });

        Assert.Equal("result", replayed);
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task ExecuteAsync_OperationThrows_ReleasesEntry()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var serializer = new JsonIdempotencySerializer();
        var service = new IdempotencyService(
            store,
            serializer,
            options,
            timeProvider);

        var key = new IdempotencyKey("orders", "123");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync<string>(
                key,
                "fingerprint",
                _ => throw new InvalidOperationException("boom")));

        string? result = await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult<string?>("retry-success"));

        Assert.Equal("retry-success", result);
    }

    [Fact]
    public async Task ExecuteAsync_SameKeyDifferentFingerprint_ThrowsConflictException()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var serializer = new JsonIdempotencySerializer();
        var service = new IdempotencyService(
            store,
            serializer,
            options,
            timeProvider);

        var key = new IdempotencyKey("orders", "123");

        await service.ExecuteAsync(
            key,
            "fingerprint-a",
            _ => Task.FromResult<string?>("result"));

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            service.ExecuteAsync(
                key,
                "fingerprint-b",
                _ => Task.FromResult<string?>("other-result")));
    }

    [Fact]
    public async Task ExecuteAsync_OperationAlreadyInProgress_ThrowsInProgressException()
    {
        var options = new IdempotencyOptions();
        TimeProvider timeProvider = TimeProvider.System;

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var serializer = new JsonIdempotencySerializer();
        var service = new IdempotencyService(
            store,
            serializer,
            options,
            timeProvider);

        var key = new IdempotencyKey("orders", "123");

        var operationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var allowCompletion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<string?> firstExecution = service.ExecuteAsync(
            key,
            "fingerprint",
            async _ =>
            {
                operationStarted.SetResult(true);
                await allowCompletion.Task;
                return "result";
            });

        await operationStarted.Task;

        await Assert.ThrowsAsync<IdempotencyInProgressException>(() =>
            service.ExecuteAsync(
                key,
                "fingerprint",
                _ => Task.FromResult<string?>("second")));

        allowCompletion.SetResult(true);

        Assert.Equal("result", await firstExecution);
    }
}