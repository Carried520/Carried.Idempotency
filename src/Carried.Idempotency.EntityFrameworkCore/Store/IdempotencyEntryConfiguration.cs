using Carried.Idempotency.EntityFrameworkCore.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carried.Idempotency.EntityFrameworkCore.Store;

internal sealed class IdempotencyEntryConfiguration(EntityFrameworkCoreIdempotencyOptions options)
    : IEntityTypeConfiguration<IdempotencyEntry>
{
    private readonly EntityFrameworkCoreIdempotencyOptions _options =
        options ?? throw new ArgumentNullException(nameof(options));

    public void Configure(EntityTypeBuilder<IdempotencyEntry> builder)
    {
        builder.ToTable(_options.TableName, _options.Schema);

        builder.HasKey(x => new
        {
            x.Scope,
            x.Key
        });

        builder.Property(x => x.Scope)
            .HasMaxLength(IdempotencyEntry.MaxKeyPartLength)
            .IsRequired();

        builder.Property(x => x.Key)
            .HasMaxLength(IdempotencyEntry.MaxKeyPartLength)
            .IsRequired();

        builder.Property(x => x.Fingerprint)
            .IsRequired();

        builder.Property(x => x.OwnerToken);

        builder.Property(x => x.State)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Payload);

        builder.Property(x => x.ExpiresAt)
            .IsRequired();
    }
}