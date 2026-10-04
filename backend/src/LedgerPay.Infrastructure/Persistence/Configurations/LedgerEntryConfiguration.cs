using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerPay.Infrastructure.Persistence.Configurations;

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", table =>
        {
            table.HasCheckConstraint("CK_LedgerEntries_Amounts_NonNegative", "[Debit] >= 0 AND [Credit] >= 0");
            table.HasCheckConstraint(
                "CK_LedgerEntries_ExactlyOneSide",
                "([Debit] > 0 AND [Credit] = 0) OR ([Debit] = 0 AND [Credit] > 0)");
        });

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Sequence).ValueGeneratedOnAdd().UseIdentityColumn();
        builder.Property(entry => entry.Debit).HasPrecision(18, 2);
        builder.Property(entry => entry.Credit).HasPrecision(18, 2);
        builder.Property(entry => entry.CreatedAt).HasColumnType("datetime2");

        // Serves the statement view, which runs one account in Sequence order, and carries the columns it reads.
        builder.HasIndex(entry => new { entry.LedgerAccountId, entry.Sequence })
            .IncludeProperties(entry => new { entry.CreatedAt, entry.Debit, entry.Credit, entry.TransactionId });

        builder.HasOne(entry => entry.Transaction)
            .WithMany(transaction => transaction.Entries)
            .HasForeignKey(entry => entry.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entry => entry.LedgerAccount)
            .WithMany()
            .HasForeignKey(entry => entry.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
