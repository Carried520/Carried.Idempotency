using System.Text.Json;
using Carried.Idempotency.Exceptions;
using Carried.Idempotency.IdempotencyEvents;
using Carried.Idempotency.IdempotencyOperation;
using Carried.Idempotency.Options;
using Carried.Idempotency.Serialization;
using Carried.Idempotency.Store;

namespace Carried.Idempotency.Tests;

public sealed class IdempotencyServiceEventTests
{
    private static readonly IdempotencyKey Key = new("test", "key");
    private const string Fingerprint = "fingerprint";

    [Fact]
    public async Task ExecuteAsync_CompletedOperation_EmitsAcquiredThenCompleted()
    {
        var store = new TestStore();
        IdempotencyService service = CreateService(store);

        var events = new List<string>();

        service.Acquired += (_, _) => events.Add("Acquired");
        service.Completed += (_, _) => events.Add("Completed");

        string? result = await service.ExecuteAsync(
            Key,
            Fingerprint,
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        Assert.Equal("result", result);
        Assert.Equal(
            ["Acquired", "Completed"],
            events);
    }

    [Fact]
    public async Task ExecuteAsync_ReleasedOperation_EmitsAcquiredThenReleased()
    {
        var store = new TestStore();
        IdempotencyService service = CreateService(store);

        var events = new List<string>();

        service.Acquired += (_, _) => events.Add("Acquired");
        service.Released += (_, _) => events.Add("Released");

        string? result = await service.ExecuteAsync(
            Key,
            Fingerprint,
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Release("result")));

        Assert.Equal("result", result);
        Assert.Equal(
            ["Acquired", "Released"],
            events);
    }

    [Fact]
    public async Task ExecuteAsync_CompletedEntry_EmitsReplayed()
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes("stored");

        var store = new TestStore
        {
            AcquireResult = IdempotencyAcquireResult.Completed(payload)
        };

        IdempotencyService service = CreateService(store);

        IdempotencyReplayedEvent? received = null;

        service.Replayed += (_, @event) => received = @event;

        string? result = await service.ExecuteAsync<string>(
            Key,
            Fingerprint,
            _ => throw new InvalidOperationException(
                "Operation should not execute."));

        Assert.Equal("stored", result);
        Assert.NotNull(received);
        Assert.Equal(Key, received.Key);
    }

    [Fact]
    public async Task ExecuteAsync_Conflict_EmitsConflict()
    {
        var store = new TestStore
        {
            AcquireResult = IdempotencyAcquireResult.Conflict()
        };

        IdempotencyService service = CreateService(store);

        IdempotencyConflictEvent? received = null;

        service.Conflict += (_, @event) => received = @event;

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            service.ExecuteAsync<string>(
                Key,
                Fingerprint,
                _ => throw new InvalidOperationException(
                    "Operation should not execute.")));

        Assert.NotNull(received);
        Assert.Equal(Key, received.Key);
    }

    [Fact]
    public async Task ExecuteAsync_InProgress_EmitsInProgress()
    {
        var store = new TestStore
        {
            AcquireResult = IdempotencyAcquireResult.InProgress()
        };

        IdempotencyService service = CreateService(store);

        IdempotencyInProgressEvent? received = null;

        service.InProgress += (_, @event) => received = @event;

        await Assert.ThrowsAsync<IdempotencyInProgressException>(() =>
            service.ExecuteAsync<string>(
                Key,
                Fingerprint,
                _ => throw new InvalidOperationException(
                    "Operation should not execute.")));

        Assert.NotNull(received);
        Assert.Equal(Key, received.Key);
    }

    [Fact]
    public async Task ExecuteAsync_FailedStateTransition_EmitsLeaseLost()
    {
        var store = new TestStore
        {
            CompleteResult = false
        };

        IdempotencyService service = CreateService(store);

        var events = new List<string>();

        service.Acquired += (_, _) => events.Add("Acquired");
        service.LeaseLost += (_, _) => events.Add("LeaseLost");

        await Assert.ThrowsAsync<IdempotencyLeaseLostException>(() =>
            service.ExecuteAsync(
                Key,
                Fingerprint,
                _ => Task.FromResult(
                    IdempotencyOperationResult<string>.Complete("result"))));

        Assert.Equal(
            ["Acquired", "LeaseLost"],
            events);
    }

    [Fact]
    public async Task ExecuteAsync_OperationThrowsAndCleanupSucceeds_EmitsReleased()
    {
        var store = new TestStore();
        IdempotencyService service = CreateService(store);

        var events = new List<string>();

        service.Acquired += (_, _) => events.Add("Acquired");
        service.Released += (_, _) => events.Add("Released");

        var exception = new TestException();

        TestException thrown = await Assert.ThrowsAsync<TestException>(() =>
            service.ExecuteAsync<string>(
                Key,
                Fingerprint,
                _ => Task.FromException<IdempotencyOperationResult<string>>(
                    exception)));

        Assert.Same(exception, thrown);

        Assert.Equal(
            ["Acquired", "Released"],
            events);
    }

    [Fact]
    public async Task ExecuteAsync_CleanupThrows_EmitsReleaseFailedAndPreservesOperationException()
    {
        var releaseException = new InvalidOperationException(
            "Release failed.");

        var store = new TestStore
        {
            ReleaseException = releaseException
        };

        IdempotencyService service = CreateService(store);

        IdempotencyReleaseFailedEvent? received = null;

        service.ReleaseFailed += (_, @event) => received = @event;

        var operationException = new TestException();

        TestException thrown = await Assert.ThrowsAsync<TestException>(() =>
            service.ExecuteAsync<string>(
                Key,
                Fingerprint,
                _ => Task.FromException<IdempotencyOperationResult<string>>(
                    operationException)));

        Assert.Same(operationException, thrown);

        Assert.NotNull(received);
        Assert.Equal(Key, received.Key);
        Assert.Same(releaseException, received.Exception);
    }

    [Fact]
    public async Task EventSubscriberThrows_DoesNotAffectExecutionOrOtherSubscribers()
    {
        var store = new TestStore();
        IdempotencyService service = CreateService(store);

        bool secondSubscriberCalled = false;

        service.Completed += (_, _) =>
            throw new InvalidOperationException(
                "Subscriber failed.");

        service.Completed += (_, _) =>
            secondSubscriberCalled = true;

        string? result = await service.ExecuteAsync(
            Key,
            Fingerprint,
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        Assert.Equal("result", result);
        Assert.True(secondSubscriberCalled);
    }
    
    
    [Fact]
    public async Task ExecuteAsync_HeartbeatLosesLease_EmitsLeaseLost()
    {
        var store = new TestStore
        {
            RenewResult = false
        };

        IdempotencyService service = IdempotencyService.Create(
            store,
            new TestSerializer(),
            new IdempotencyOptions
            {
                LeaseDuration = TimeSpan.FromMilliseconds(30),
                CompletedRetention = TimeSpan.FromHours(1)
            });

        var events = new List<string>();

        service.Acquired += (_, _) => events.Add("Acquired");
        service.LeaseLost += (_, _) => events.Add("LeaseLost");

        await Assert.ThrowsAsync<IdempotencyLeaseLostException>(() =>
            service.ExecuteAsync<string>(
                Key,
                Fingerprint,
                async cancellationToken =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    return IdempotencyOperationResult<string>.Complete("result");
                }));

        Assert.Equal(
            ["Acquired", "LeaseLost"],
            events);
    }

    private static IdempotencyService CreateService(
        TestStore store)
    {
        return IdempotencyService.Create(
            store,
            new TestSerializer(),
            new IdempotencyOptions
            {
                LeaseDuration = TimeSpan.FromMinutes(5),
                CompletedRetention = TimeSpan.FromHours(1)
            });
    }

    private sealed class TestStore : IIdempotencyStore
    {
        private static readonly Guid OwnerToken =
            Guid.NewGuid();

        public IdempotencyAcquireResult AcquireResult { get; set; } =
            IdempotencyAcquireResult.Acquired(OwnerToken);

        public bool CompleteResult { get; set; } = true;

        public bool ReleaseResult { get; set; } = true;

        public bool RenewResult { get; set; } = true;

        public Exception? ReleaseException { get; set; }

        public ValueTask<IdempotencyAcquireResult> TryAcquireAsync(
            IdempotencyKey key,
            string fingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(AcquireResult);
        }

        public ValueTask<bool> TryCompleteAsync(
            IdempotencyKey key,
            Guid ownerToken,
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(CompleteResult);
        }

        public ValueTask<bool> TryReleaseAsync(
            IdempotencyKey key,
            Guid ownerToken,
            CancellationToken cancellationToken = default)
        {
            if (ReleaseException is not null)
                return ValueTask.FromException<bool>(
                    ReleaseException);

            return ValueTask.FromResult(ReleaseResult);
        }

        public ValueTask<bool> TryRenewLeaseAsync(
            IdempotencyKey key,
            Guid ownerToken,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(RenewResult);
        }
    }

    private sealed class TestSerializer : IIdempotencySerializer
    {
        public byte[] Serialize<T>(T? value)
        {
            return JsonSerializer.SerializeToUtf8Bytes(value);
        }

        public T? Deserialize<T>(byte[] payload)
        {
            return JsonSerializer.Deserialize<T>(payload);
        }
    }

    private sealed class TestException : Exception;
}