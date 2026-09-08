using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.EntityFrameworkCore;
using Carried.Idempotency.EntityFrameworkCore.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carried.Idempotency.EFCore.Tests;

public sealed class EntityFrameworkCoreIntegrationTests
{
    [Fact]
    public async Task UseDbContext_RegistersResolvableIdempotencyService()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var services = new ServiceCollection();

        services.AddDbContext<TestDbContext>(options =>
            options.UseSqlite(connection));

        services.AddIdempotency(builder =>
        {
            builder.UseDbContext<TestDbContext>();
        });

        await using ServiceProvider provider =
            services.BuildServiceProvider();

        await using AsyncServiceScope scope =
            provider.CreateAsyncScope();

        var service =
            scope.ServiceProvider.GetRequiredService<IdempotencyService>();

        Assert.NotNull(service);
    }

    [Fact]
    public async Task UseDbContext_WorksWithPooledDbContext()
    {
        var connectionString =
            $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";

        await using var keeper =
            new SqliteConnection(connectionString);

        await keeper.OpenAsync();

        var services = new ServiceCollection();

        services.AddDbContextPool<TestDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddIdempotency(builder =>
        {
            builder.UseDbContext<TestDbContext>();
        });

        await using ServiceProvider provider =
            services.BuildServiceProvider();

        await using AsyncServiceScope scope =
            provider.CreateAsyncScope();

        var context =
            scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await context.Database.EnsureCreatedAsync();

        var service =
            scope.ServiceProvider.GetRequiredService<IdempotencyService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void UseDbContext_WhenDbContextIsNotRegistered_Throws()
    {
        var services = new ServiceCollection();

        services.AddIdempotency(builder =>
        {
            builder.UseDbContext<TestDbContext>();
        });

        using ServiceProvider provider =
            services.BuildServiceProvider();

        using IServiceScope scope =
            provider.CreateScope();

        Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IdempotencyService>());
    }

    [Fact]
    public async Task CreateEfCore_CreatesWorkingService()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        DbContextOptions<TestDbContext> dbOptions =
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new TestDbContext(dbOptions);

        await context.Database.EnsureCreatedAsync();

        var service =
            IdempotencyService.CreateEfCore(context);

        Assert.NotNull(service);
    }

    [Fact]
    public async Task CreateEfCore_DoesNotOwnDbContextLifetime()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        DbContextOptions<TestDbContext> dbOptions =
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new TestDbContext(dbOptions);

        await context.Database.EnsureCreatedAsync();

        var service =
            IdempotencyService.CreateEfCore(context);

        Assert.NotNull(service);
        
        bool canConnect =
            await context.Database.CanConnectAsync();

        Assert.True(canConnect);
    }

    [Fact]
    public async Task UseDbContext_UsesRegisteredTimeProvider()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

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

        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(timeProvider);

        services.AddDbContext<TestDbContext>(options =>
            options.UseSqlite(connection));

        services.AddIdempotency(builder =>
        {
            builder.UseDbContext<TestDbContext>();
        });

        await using ServiceProvider provider =
            services.BuildServiceProvider();

        await using AsyncServiceScope scope =
            provider.CreateAsyncScope();

        var context =
            scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await context.Database.EnsureCreatedAsync();

        var service =
            scope.ServiceProvider.GetRequiredService<IdempotencyService>();

        Assert.NotNull(service);
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

    private sealed class ManualTimeProvider(
        DateTimeOffset utcNow)
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