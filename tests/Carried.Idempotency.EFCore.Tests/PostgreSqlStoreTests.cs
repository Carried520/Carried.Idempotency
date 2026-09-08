using Carried.Idempotency.EntityFrameworkCore.Extensions;
using Carried.Idempotency.EntityFrameworkCore.Store;
using Carried.Idempotency.Options;
using Carried.Idempotency.Store;
using Microsoft.EntityFrameworkCore;

namespace Carried.Idempotency.EFCore.Tests;

public sealed class PostgreSqlStoreTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=carried_idempotency_tests;Username=postgres;Password=root";

    [Fact]
    public async Task TryAcquireAsync_WhenMissing_Acquires()
    {
        await ResetDatabaseAsync();

        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        var key = new IdempotencyKey("scope", "key");

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(
            IdempotencyAcquireStatus.Acquired,
            result.Status);

        Assert.NotNull(result.OwnerToken);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenAlreadyInProgressWithSameFingerprint_ReturnsInProgress()
    {
        await ResetDatabaseAsync();

        var key = new IdempotencyKey("scope", "key");

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult first =
                await store.TryAcquireAsync(key, "fingerprint");

            Assert.Equal(
                IdempotencyAcquireStatus.Acquired,
                first.Status);
        }

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult second =
                await store.TryAcquireAsync(key, "fingerprint");

            Assert.Equal(
                IdempotencyAcquireStatus.InProgress,
                second.Status);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_WhenFingerprintDiffers_ReturnsConflict()
    {
        await ResetDatabaseAsync();

        var key = new IdempotencyKey("scope", "key");

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            await store.TryAcquireAsync(
                key,
                "fingerprint-1");
        }

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult result =
                await store.TryAcquireAsync(
                    key,
                    "fingerprint-2");

            Assert.Equal(
                IdempotencyAcquireStatus.Conflict,
                result.Status);
        }
    }

    [Fact]
    public async Task TryCompleteAsync_ThenAcquire_ReplaysCompletedResult()
    {
        await ResetDatabaseAsync();

        var key = new IdempotencyKey("scope", "key");
        byte[] payload = [1, 2, 3];

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(
                    key,
                    "fingerprint");

            Guid ownerToken = acquired.OwnerToken
                              ?? throw new InvalidOperationException(
                                  "Acquire result did not contain an owner token.");

            bool completed =
                await store.TryCompleteAsync(
                    key,
                    ownerToken,
                    payload);

            Assert.True(completed);
        }

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult replay =
                await store.TryAcquireAsync(
                    key,
                    "fingerprint");

            Assert.Equal(
                IdempotencyAcquireStatus.Completed,
                replay.Status);

            Assert.Equal(
                payload,
                replay.Payload);
        }
    }

    [Fact]
    public async Task ExpiredLease_CanBeReacquired()
    {
        await ResetDatabaseAsync();

        var timeProvider =
            new ManualTimeProvider(
                new DateTimeOffset(
                    2026,
                    1,
                    1,
                    0,
                    0,
                    0,
                    TimeSpan.Zero));

        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(10)
        };

        var key = new IdempotencyKey("scope", "key");

        Guid firstOwner;

        await using (TestDbContext context = CreateContext())
        {
            var store =
                new EntityFrameworkCoreStore<TestDbContext>(
                    context,
                    options,
                    timeProvider);

            IdempotencyAcquireResult first =
                await store.TryAcquireAsync(
                    key,
                    "fingerprint");

            firstOwner =
                first.OwnerToken
                ?? throw new InvalidOperationException();
        }

        timeProvider.Advance(
            TimeSpan.FromSeconds(11));

        await using (TestDbContext context = CreateContext())
        {
            var store =
                new EntityFrameworkCoreStore<TestDbContext>(
                    context,
                    options,
                    timeProvider);

            IdempotencyAcquireResult second =
                await store.TryAcquireAsync(
                    key,
                    "fingerprint");

            Assert.Equal(
                IdempotencyAcquireStatus.Acquired,
                second.Status);

            Assert.NotNull(second.OwnerToken);
            Assert.NotEqual(
                firstOwner,
                second.OwnerToken);
        }
    }

    [Fact]
    public async Task StaleOwner_CannotCompleteAfterTakeover()
    {
        await ResetDatabaseAsync();

        var timeProvider =
            new ManualTimeProvider(
                new DateTimeOffset(
                    2026,
                    1,
                    1,
                    0,
                    0,
                    0,
                    TimeSpan.Zero));

        var options = new IdempotencyOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(10)
        };

        var key = new IdempotencyKey("scope", "key");

        Guid firstOwner;

        await using (TestDbContext context = CreateContext())
        {
            var store =
                new EntityFrameworkCoreStore<TestDbContext>(
                    context,
                    options,
                    timeProvider);

            IdempotencyAcquireResult first =
                await store.TryAcquireAsync(
                    key,
                    "fingerprint");

            firstOwner =
                first.OwnerToken
                ?? throw new InvalidOperationException();
        }

        timeProvider.Advance(
            TimeSpan.FromSeconds(11));

        await using (TestDbContext context = CreateContext())
        {
            var store =
                new EntityFrameworkCoreStore<TestDbContext>(
                    context,
                    options,
                    timeProvider);

            IdempotencyAcquireResult second =
                await store.TryAcquireAsync(
                    key,
                    "fingerprint");

            Assert.Equal(
                IdempotencyAcquireStatus.Acquired,
                second.Status);
        }

        await using (TestDbContext context = CreateContext())
        {
            var store =
                new EntityFrameworkCoreStore<TestDbContext>(
                    context,
                    options,
                    timeProvider);

            bool completed =
                await store.TryCompleteAsync(
                    key,
                    firstOwner,
                    [1, 2, 3]);

            Assert.False(completed);
        }
    }

    [Fact]
    public async Task ConcurrentAcquire_WithSameFingerprint_HasSingleWinner()
    {
        await ResetDatabaseAsync();

        var key =
            new IdempotencyKey(
                "scope",
                Guid.NewGuid().ToString());

        Task<IdempotencyAcquireResult>[] tasks =
            Enumerable.Range(0, 10)
                .Select(_ => AcquireAsync(
                    key,
                    "fingerprint"))
                .ToArray();

        IdempotencyAcquireResult[] results =
            await Task.WhenAll(tasks);

        Assert.Single(results, x => x.Status ==
                     IdempotencyAcquireStatus.Acquired);

        Assert.Equal(
            9,
            results.Count(
                x => x.Status ==
                     IdempotencyAcquireStatus.InProgress));
    }

    [Fact]
    public async Task ConcurrentAcquire_WithDifferentFingerprints_HasSingleWinner()
    {
        await ResetDatabaseAsync();

        var key =
            new IdempotencyKey(
                "scope",
                Guid.NewGuid().ToString());

        Task<IdempotencyAcquireResult>[] tasks =
            Enumerable.Range(0, 10)
                .Select(i => AcquireAsync(
                    key,
                    $"fingerprint-{i}"))
                .ToArray();

        IdempotencyAcquireResult[] results =
            await Task.WhenAll(tasks);

        Assert.Single(results, x => x.Status ==
                     IdempotencyAcquireStatus.Acquired);

        Assert.Equal(
            9,
            results.Count(
                x => x.Status ==
                     IdempotencyAcquireStatus.Conflict));
    }

    private static async Task<IdempotencyAcquireResult> AcquireAsync(
        IdempotencyKey key,
        string fingerprint)
    {
        await using TestDbContext context =
            CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store =
            CreateStore(context);

        return await store.TryAcquireAsync(
            key,
            fingerprint);
    }

    private static EntityFrameworkCoreStore<TestDbContext>
        CreateStore(TestDbContext context)
    {
        return new EntityFrameworkCoreStore<TestDbContext>(
            context,
            new IdempotencyOptions());
    }

    private static TestDbContext CreateContext()
    {
        DbContextOptions<TestDbContext> options =
            new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

        return new TestDbContext(options);
    }

    private static async Task ResetDatabaseAsync()
    {
        await using TestDbContext context =
            CreateContext();

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.AddIdempotency();
        }
    }

    private sealed class ManualTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
            => _utcNow;

        public void Advance(TimeSpan duration)
            => _utcNow += duration;
    }
}