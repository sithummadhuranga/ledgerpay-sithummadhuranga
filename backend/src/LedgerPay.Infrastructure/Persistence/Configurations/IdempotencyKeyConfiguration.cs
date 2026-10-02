using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerPay.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("IdempotencyKeys");
        builder.HasKey(key => key.Id);
        builder.Property(key => key.Key).HasMaxLength(100).IsUnicode(false);
        builder.Property(key => key.Endpoint).HasMaxLength(100).IsUnicode(false);
        builder.Property(key => key.RequestHash).HasMaxLength(64).IsUnicode(false);
        builder.Property(key => key.CreatedAt).HasColumnType("datetime2");

        // A concurrent duplicate blocks on this index until the first request commits.
        builder.HasIndex(key => new { key.UserId, key.Key, key.Endpoint }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(key => key.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(key => key.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
