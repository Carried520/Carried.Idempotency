using Carried.Idempotency.Exceptions;
using Carried.Idempotency.Options;
using Carried.Idempotency.Serialization;
using Carried.Idempotency.Store;
using Microsoft.Extensions.Time.Testing;

namespace Carried.Idempotency.Tests;

public partial class IdempotencyServiceTests
{
    [Fact]
    public async Task ExecuteAsync_LongRunningOperation_RenewsLease()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(3)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var store = new InMemoryIdempotencyStore(options, timeProvider);
        var serializer = new JsonIdempotencySerializer();

        var service = new IdempotencyService(
            store,
            serializer,
            options,
            timeProvider);

        var key = new IdempotencyKey("orders", "123");

        var operationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var allowCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Task<string?> execution = service.ExecuteAsync(
            key,
            "fingerprint",
            async cancellationToken =>
            {
                operationStarted.SetResult();

                await allowCompletion.Task.WaitAsync(cancellationToken);

                return "result";
            });

        await operationStarted.Task;
        
        for (int i = 0; i < 4; i++)
        {
            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await Task.Yield();
        }


        IdempotencyAcquireResult acquire =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(
            IdempotencyAcquireStatus.InProgress,
            acquire.Status);

        allowCompletion.SetResult();

        Assert.Equal("result", await execution);
    }

    [Fact]
    public async Task ExecuteAsync_LeaseLost_CancelsOperationAndThrowsLeaseLostException()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(3)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var innerStore = new InMemoryIdempotencyStore(options, timeProvider);
        var store = new LeaseRenewalFailingStore(innerStore);

        var serializer = new JsonIdempotencySerializer();

        var service = new IdempotencyService(
            store,
            serializer,
            options,
            timeProvider);

        var key = new IdempotencyKey("orders", "123");

        var operationStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<string?> execution = service.ExecuteAsync(
            key,
            "fingerprint",
            async cancellationToken =>
            {
                operationStarted.SetResult();

                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);

                return "never";
            });

        await operationStarted.Task;

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<IdempotencyLeaseLostException>(
            () => execution);
    }

    [Fact]
    public async Task ExecuteAsync_HeartbeatThrows_PropagatesHeartbeatException()
    {
        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromMinutes(3)
        };

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var innerStore = new InMemoryIdempotencyStore(options, timeProvider);
        var store = new LeaseRenewalThrowingStore(innerStore);

        var serializer = new JsonIdempotencySerializer();

        var service = new IdempotencyService(
            store,
            serializer,
            options,
            timeProvider);

        var key = new IdempotencyKey("orders", "123");

        var operationStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<string?> execution = service.ExecuteAsync(
            key,
            "fingerprint",
            async cancellationToken =>
            {
                operationStarted.SetResult();

                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);

                return "never";
            });

        await operationStarted.Task;

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => execution);

        Assert.Equal("heartbeat failure", exception.Message);
    }

    private sealed class LeaseRenewalFailingStore : IIdempotencyStore
    {
        private readonly IIdempotencyStore _inner;

        public LeaseRenewalFailingStore(IIdempotencyStore inner)
        {
            _inner = inner;
        }

        public ValueTask<IdempotencyAcquireResult> TryAcquireAsync(
            IdempotencyKey key,
            string fingerprint,
            CancellationToken cancellationToken = default)
        {
            return _inner.TryAcquireAsync(
                key,
                fingerprint,
                cancellationToken);
        }

        public ValueTask<bool> TryCompleteAsync(
            IdempotencyKey key,
            Guid ownerToken,
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            return _inner.TryCompleteAsync(
                key,
                ownerToken,
                payload,
                cancellationToken);
        }

        public ValueTask<bool> TryReleaseAsync(
            IdempotencyKey key,
            Guid ownerToken,
            CancellationToken cancellationToken = default)
        {
            return _inner.TryReleaseAsync(
                key,
                ownerToken,
                cancellationToken);
        }

        public ValueTask<bool> TryRenewLeaseAsync(
            IdempotencyKey key,
            Guid ownerToken,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(false);
        }
    }

    private sealed class LeaseRenewalThrowingStore : IIdempotencyStore
    {
        private readonly IIdempotencyStore _inner;

        public LeaseRenewalThrowingStore(IIdempotencyStore inner)
        {
            _inner = inner;
        }

        public ValueTask<IdempotencyAcquireResult> TryAcquireAsync(
            IdempotencyKey key,
            string fingerprint,
            CancellationToken cancellationToken = default)
        {
            return _inner.TryAcquireAsync(
                key,
                fingerprint,
                cancellationToken);
        }

        public ValueTask<bool> TryCompleteAsync(
            IdempotencyKey key,
            Guid ownerToken,
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            return _inner.TryCompleteAsync(
                key,
                ownerToken,
                payload,
                cancellationToken);
        }

        public ValueTask<bool> TryReleaseAsync(
            IdempotencyKey key,
            Guid ownerToken,
            CancellationToken cancellationToken = default)
        {
            return _inner.TryReleaseAsync(
                key,
                ownerToken,
                cancellationToken);
        }

        public ValueTask<bool> TryRenewLeaseAsync(
            IdempotencyKey key,
            Guid ownerToken,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("heartbeat failure");
        }
    }
}