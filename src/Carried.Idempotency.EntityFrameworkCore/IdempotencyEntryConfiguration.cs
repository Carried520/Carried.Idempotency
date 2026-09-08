using Carried.Idempotency.EntityFrameworkCore.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carried.Idempotency.EntityFrameworkCore;

internal sealed class IdempotencyEntryConfiguration(EntityFrameworkCoreIdempotencyOptions options)
    : IEntityTypeConfiguration<IdempotencyEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyEntry> builder)
    {
        builder.ToTable(options.TableName, options.Schema);

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
            .IsRequired();

        builder.Property(x => x.Payload);

        builder.Property(x => x.ExpiresAt)
            .IsRequired();
    }
}