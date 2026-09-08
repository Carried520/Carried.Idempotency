using Carried.Idempotency.EntityFrameworkCore;
using Carried.Idempotency.EntityFrameworkCore.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Carried.Idempotency.EFCore.Tests;

public sealed class ModelBuilderExtensionsTests
{
    [Fact]
    public void AddIdempotency_UsesDefaultTableName()
    {
        var options =
            new DbContextOptionsBuilder<DefaultTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

        using var context =
            new DefaultTestDbContext(options);

        IEntityType entityType =
            GetIdempotencyEntityType(context);

        Assert.Equal(
            "__CarriedIdempotency",
            entityType.GetTableName());
    }

    [Fact]
    public void AddIdempotency_UsesConfiguredTableName()
    {
        var options =
            new DbContextOptionsBuilder<CustomTableTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

        using var context =
            new CustomTableTestDbContext(options);

        IEntityType entityType =
            GetIdempotencyEntityType(context);

        Assert.Equal(
            "IdempotencyEntries",
            entityType.GetTableName());
    }

    [Fact]
    public void AddIdempotency_UsesConfiguredSchema()
    {
        var options =
            new DbContextOptionsBuilder<CustomSchemaTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

        using var context =
            new CustomSchemaTestDbContext(options);

        IEntityType entityType =
            GetIdempotencyEntityType(context);

        Assert.Equal(
            "infra",
            entityType.GetSchema());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void AddIdempotency_WhenTableNameIsEmpty_Throws(
        string tableName)
    {
        var options =
            new DbContextOptionsBuilder<InvalidTableNameTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

        using var context =
            new InvalidTableNameTestDbContext(
                options,
                tableName);

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
            {
                _ = context.Model;
            });

        Assert.Contains(
            "Table name cannot be empty",
            exception.Message);
    }

    [Fact]
    public void AddIdempotency_ConfiguresCompositePrimaryKey()
    {
        var options =
            new DbContextOptionsBuilder<DefaultTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

        using var context =
            new DefaultTestDbContext(options);

        IEntityType entityType =
            GetIdempotencyEntityType(context);

        IKey primaryKey =
            entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException(
                "Idempotency primary key was not configured.");

        string[] propertyNames =
            primaryKey.Properties
                .Select(x => x.Name)
                .ToArray();

        Assert.Equal(
            ["Scope", "Key"],
            propertyNames);
    }

    [Fact]
    public void AddIdempotency_ConfiguresScopeMaxLength()
    {
        var options =
            new DbContextOptionsBuilder<DefaultTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

        using var context =
            new DefaultTestDbContext(options);

        IEntityType entityType =
            GetIdempotencyEntityType(context);

        IProperty scope =
            entityType.FindProperty(nameof(IdempotencyEntry.Scope))
            ?? throw new InvalidOperationException(
                "Scope property was not mapped.");

        Assert.Equal(
            IdempotencyEntry.MaxKeyPartLength,
            scope.GetMaxLength());
    }

    [Fact]
    public void AddIdempotency_ConfiguresKeyMaxLength()
    {
        var options =
            new DbContextOptionsBuilder<DefaultTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

        using var context =
            new DefaultTestDbContext(options);

        IEntityType entityType =
            GetIdempotencyEntityType(context);

        IProperty key =
            entityType.FindProperty(nameof(IdempotencyEntry.Key))
            ?? throw new InvalidOperationException(
                "Key property was not mapped.");

        Assert.Equal(
            IdempotencyEntry.MaxKeyPartLength,
            key.GetMaxLength());
    }

    [Fact]
    public void AddIdempotency_ConfiguresRequiredProperties()
    {
        var options =
            new DbContextOptionsBuilder<DefaultTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

        using var context =
            new DefaultTestDbContext(options);

        IEntityType entityType =
            GetIdempotencyEntityType(context);

        Assert.False(
            entityType
                .FindProperty(nameof(IdempotencyEntry.Scope))!
                .IsNullable);

        Assert.False(
            entityType
                .FindProperty(nameof(IdempotencyEntry.Key))!
                .IsNullable);

        Assert.False(
            entityType
                .FindProperty(nameof(IdempotencyEntry.Fingerprint))!
                .IsNullable);

        Assert.False(
            entityType
                .FindProperty(nameof(IdempotencyEntry.State))!
                .IsNullable);

        Assert.False(
            entityType
                .FindProperty(nameof(IdempotencyEntry.ExpiresAt))!
                .IsNullable);
    }

    [Fact]
    public void AddIdempotency_AllowsNullableOwnerTokenAndPayload()
    {
        var options =
            new DbContextOptionsBuilder<DefaultTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

        using var context =
            new DefaultTestDbContext(options);

        IEntityType entityType =
            GetIdempotencyEntityType(context);

        Assert.True(
            entityType
                .FindProperty(nameof(IdempotencyEntry.OwnerToken))!
                .IsNullable);

        Assert.True(
            entityType
                .FindProperty(nameof(IdempotencyEntry.Payload))!
                .IsNullable);
    }

    private static IEntityType GetIdempotencyEntityType(
        DbContext context)
    {
        return context.Model.FindEntityType(
                   typeof(IdempotencyEntry))
               ?? throw new InvalidOperationException(
                   "Idempotency entry was not mapped.");
    }

    private sealed class DefaultTestDbContext(
        DbContextOptions<DefaultTestDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.AddIdempotency();
        }
    }

    private sealed class CustomTableTestDbContext(
        DbContextOptions<CustomTableTestDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.AddIdempotency(options =>
            {
                options.TableName = "IdempotencyEntries";
            });
        }
    }

    private sealed class CustomSchemaTestDbContext(
        DbContextOptions<CustomSchemaTestDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.AddIdempotency(options =>
            {
                options.Schema = "infra";
            });
        }
    }

    private sealed class InvalidTableNameTestDbContext(
        DbContextOptions<InvalidTableNameTestDbContext> options,
        string tableName)
        : DbContext(options)
    {
        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.AddIdempotency(options =>
            {
                options.TableName = tableName;
            });
        }
    }
}