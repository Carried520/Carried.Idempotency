using Carried.Idempotency.EntityFrameworkCore.Extensions;
using Carried.Idempotency.EntityFrameworkCore.Store;
using Carried.Idempotency.Options;
using Carried.Idempotency.Store;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Carried.Idempotency.EFCore.Tests;

public sealed class EntityFrameworkCoreStoreExpiryTests : IAsyncLifetime
{
    private readonly string _connectionString =
        $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";

    private SqliteConnection _keeperConnection = null!;

    private readonly ManualTimeProvider _timeProvider =
        new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private readonly IdempotencyOptions _options = new()
    {
        LeaseDuration = TimeSpan.FromMinutes(5),
        CompletedRetention = TimeSpan.FromHours(1)
    };

    public async Task InitializeAsync()
    {
        _keeperConnection = new SqliteConnection(_connectionString);
        await _keeperConnection.OpenAsync();

        await using TestDbContext context = CreateContext();

        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _keeperConnection.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_AfterLeaseExpires_Reacquires()
    {
        IdempotencyKey key = CreateKey();
        Guid firstOwner;

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint");

            Assert.Equal(IdempotencyAcquireStatus.Acquired, acquired.Status);

            firstOwner = acquired.OwnerToken!.Value;
        }

        _timeProvider.Advance(_options.LeaseDuration);

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult reacquired =
                await store.TryAcquireAsync(key, "fingerprint");

            Assert.Equal(IdempotencyAcquireStatus.Acquired, reacquired.Status);
            Assert.NotEqual(firstOwner, reacquired.OwnerToken);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_AfterLeaseExpires_AllowsDifferentFingerprint()
    {
        IdempotencyKey key = CreateKey();

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint-a");

            Assert.Equal(IdempotencyAcquireStatus.Acquired, acquired.Status);
        }

        _timeProvider.Advance(_options.LeaseDuration);

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult reacquired =
                await store.TryAcquireAsync(key, "fingerprint-b");

            Assert.Equal(IdempotencyAcquireStatus.Acquired, reacquired.Status);
        }
    }

    [Fact]
    public async Task TryCompleteAsync_AfterLeaseExpires_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();
        Guid ownerToken;

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint");

            ownerToken = acquired.OwnerToken!.Value;
        }

        _timeProvider.Advance(_options.LeaseDuration);

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            bool completed = await store.TryCompleteAsync(
                key,
                ownerToken,
                [1, 2, 3]);

            Assert.False(completed);
        }
    }

    [Fact]
    public async Task TryReleaseAsync_AfterLeaseExpires_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();
        Guid ownerToken;

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint");

            ownerToken = acquired.OwnerToken!.Value;
        }

        _timeProvider.Advance(_options.LeaseDuration);

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            bool released =
                await store.TryReleaseAsync(key, ownerToken);

            Assert.False(released);
        }
    }

    [Fact]
    public async Task TryRenewLeaseAsync_AfterLeaseExpires_ReturnsFalse()
    {
        IdempotencyKey key = CreateKey();
        Guid ownerToken;

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint");

            ownerToken = acquired.OwnerToken!.Value;
        }

        _timeProvider.Advance(_options.LeaseDuration);

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            bool renewed =
                await store.TryRenewLeaseAsync(key, ownerToken);

            Assert.False(renewed);
        }
    }

    [Fact]
    public async Task OldOwner_CannotComplete_AfterAnotherOwnerTakesOver()
    {
        IdempotencyKey key = CreateKey();
        Guid oldOwner;

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint-a");

            oldOwner = acquired.OwnerToken!.Value;
        }

        _timeProvider.Advance(_options.LeaseDuration);

        Guid newOwner;

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint-b");

            Assert.Equal(IdempotencyAcquireStatus.Acquired, acquired.Status);

            newOwner = acquired.OwnerToken!.Value;
        }

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            bool oldOwnerCompleted = await store.TryCompleteAsync(
                key,
                oldOwner,
                [1, 2, 3]);

            Assert.False(oldOwnerCompleted);

            bool newOwnerCompleted = await store.TryCompleteAsync(
                key,
                newOwner,
                [4, 5, 6]);

            Assert.True(newOwnerCompleted);
        }
    }

    [Fact]
    public async Task CompletedEntry_AfterRetentionExpires_CanBeReacquired()
    {
        IdempotencyKey key = CreateKey();

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint-a");

            bool completed = await store.TryCompleteAsync(
                key,
                acquired.OwnerToken!.Value,
                [1, 2, 3]);

            Assert.True(completed);
        }

        _timeProvider.Advance(_options.CompletedRetention);

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult reacquired =
                await store.TryAcquireAsync(key, "fingerprint-b");

            Assert.Equal(IdempotencyAcquireStatus.Acquired, reacquired.Status);
        }
    }

    private EntityFrameworkCoreStore<TestDbContext> CreateStore(
        TestDbContext context)
    {
        return new EntityFrameworkCoreStore<TestDbContext>(
            context,
            _options,
            _timeProvider);
    }

    private TestDbContext CreateContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        return new TestDbContext(options);
    }

    private static IdempotencyKey CreateKey()
    {
        return new IdempotencyKey(
            "efcore-expiry-tests",
            Guid.NewGuid().ToString("N"));
    }

    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.AddIdempotency();
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }
}