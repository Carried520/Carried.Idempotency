using Carried.Idempotency.EntityFrameworkCore.Extensions;
using Carried.Idempotency.EntityFrameworkCore.Store;
using Carried.Idempotency.Options;
using Carried.Idempotency.Store;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Carried.Idempotency.EFCore.Tests;

public sealed class EntityFrameworkCoreStoreTests : IAsyncLifetime
{
    private readonly string _connectionString =
        $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";

    private SqliteConnection _keeperConnection = null!;

    private readonly IdempotencyOptions _options = new()
    {
        LeaseDuration = TimeSpan.FromMinutes(5),
        CompletedRetention = TimeSpan.FromHours(24)
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
    public async Task TryAcquireAsync_WhenKeyDoesNotExist_ReturnsAcquired()
    {
        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult result =
            await store.TryAcquireAsync(key, "fingerprint");

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
        Assert.NotNull(result.OwnerToken);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenSameFingerprintIsAlreadyInProgress_ReturnsInProgress()
    {
        IdempotencyKey key = CreateKey();

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint");

            Assert.Equal(IdempotencyAcquireStatus.Acquired, acquired.Status);
        }

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult result =
                await store.TryAcquireAsync(key, "fingerprint");

            Assert.Equal(IdempotencyAcquireStatus.InProgress, result.Status);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_WhenFingerprintDiffers_ReturnsConflict()
    {
        IdempotencyKey key = CreateKey();

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            await store.TryAcquireAsync(key, "fingerprint-a");
        }

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult result =
                await store.TryAcquireAsync(key, "fingerprint-b");

            Assert.Equal(IdempotencyAcquireStatus.Conflict, result.Status);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_AfterCompletion_ReturnsCompletedPayload()
    {
        IdempotencyKey key = CreateKey();
        byte[] payload = [1, 2, 3, 4];

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint");

            Guid ownerToken = acquired.OwnerToken!.Value;

            bool completed =
                await store.TryCompleteAsync(key, ownerToken, payload);

            Assert.True(completed);
        }

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult replay =
                await store.TryAcquireAsync(key, "fingerprint");

            Assert.Equal(IdempotencyAcquireStatus.Completed, replay.Status);
            Assert.Equal(payload, replay.Payload);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_AfterCompletionWithDifferentFingerprint_ReturnsConflict()
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

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult result =
                await store.TryAcquireAsync(key, "fingerprint-b");

            Assert.Equal(IdempotencyAcquireStatus.Conflict, result.Status);
        }
    }

    [Fact]
    public async Task TryCompleteAsync_WithCorrectOwner_ReturnsTrue()
    {
        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        bool result = await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.True(result);
    }

    [Fact]
    public async Task TryCompleteAsync_WithWrongOwner_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        IdempotencyKey key = CreateKey();

        await store.TryAcquireAsync(key, "fingerprint");

        bool result = await store.TryCompleteAsync(
            key,
            Guid.NewGuid(),
            [1, 2, 3]);

        Assert.False(result);
    }

    [Fact]
    public async Task TryCompleteAsync_WhenAlreadyCompleted_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        bool first = await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.True(first);

        bool second = await store.TryCompleteAsync(
            key,
            acquired.OwnerToken.Value,
            [4, 5, 6]);

        Assert.False(second);
    }

    [Fact]
    public async Task TryReleaseAsync_WithCorrectOwner_ReturnsTrue()
    {
        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        bool released = await store.TryReleaseAsync(
            key,
            acquired.OwnerToken!.Value);

        Assert.True(released);
    }

    [Fact]
    public async Task TryReleaseAsync_WithWrongOwner_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        IdempotencyKey key = CreateKey();

        await store.TryAcquireAsync(key, "fingerprint");

        bool released = await store.TryReleaseAsync(
            key,
            Guid.NewGuid());

        Assert.False(released);
    }

    [Fact]
    public async Task TryReleaseAsync_AfterRelease_AllowsReacquisition()
    {
        IdempotencyKey key = CreateKey();
        Guid firstOwner;

        await using (TestDbContext context = CreateContext())
        {
            EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);

            IdempotencyAcquireResult acquired =
                await store.TryAcquireAsync(key, "fingerprint");

            firstOwner = acquired.OwnerToken!.Value;

            bool released =
                await store.TryReleaseAsync(key, firstOwner);

            Assert.True(released);
        }

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
    public async Task TryReleaseAsync_WhenAlreadyCompleted_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        bool completed = await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.True(completed);

        bool released = await store.TryReleaseAsync(
            key,
            acquired.OwnerToken.Value);

        Assert.False(released);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WithCorrectOwner_ReturnsTrue()
    {
        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        bool renewed = await store.TryRenewLeaseAsync(
            key,
            acquired.OwnerToken!.Value);

        Assert.True(renewed);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WithWrongOwner_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        IdempotencyKey key = CreateKey();

        await store.TryAcquireAsync(key, "fingerprint");

        bool renewed = await store.TryRenewLeaseAsync(
            key,
            Guid.NewGuid());

        Assert.False(renewed);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WhenAlreadyCompleted_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();

        EntityFrameworkCoreStore<TestDbContext> store = CreateStore(context);
        IdempotencyKey key = CreateKey();

        IdempotencyAcquireResult acquired =
            await store.TryAcquireAsync(key, "fingerprint");

        bool completed = await store.TryCompleteAsync(
            key,
            acquired.OwnerToken!.Value,
            [1, 2, 3]);

        Assert.True(completed);

        bool renewed = await store.TryRenewLeaseAsync(
            key,
            acquired.OwnerToken.Value);

        Assert.False(renewed);
    }

    private EntityFrameworkCoreStore<TestDbContext> CreateStore(
        TestDbContext context,
        TimeProvider? timeProvider = null)
    {
        return new EntityFrameworkCoreStore<TestDbContext>(
            context,
            _options,
            timeProvider);
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
            "efcore-tests",
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
}