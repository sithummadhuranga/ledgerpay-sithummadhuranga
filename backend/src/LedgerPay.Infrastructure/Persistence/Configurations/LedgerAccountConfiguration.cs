using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerPay.Infrastructure.Persistence.Configurations;

internal sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.ToTable("LedgerAccounts", table =>
        {
            table.HasCheckConstraint("CK_LedgerAccounts_Type_Valid", CheckSql.In<LedgerAccountType>("Type"));
        });

        builder.HasKey(account => account.Id);
        builder.Property(account => account.Code).HasMaxLength(50).IsUnicode(false);
        builder.Property(account => account.Name).HasMaxLength(100);
        builder.Property(account => account.Type).HasConversion<string>().HasMaxLength(10).IsUnicode(false);

        builder.HasIndex(account => account.Code).IsUnique();

        // Filtered because system accounts have no wallet, and SQL Server allows only one NULL in a plain unique index.
        builder.HasIndex(account => account.WalletId).IsUnique().HasFilter("[WalletId] IS NOT NULL");

        builder.HasOne(account => account.Wallet)
            .WithMany()
            .HasForeignKey(account => account.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
