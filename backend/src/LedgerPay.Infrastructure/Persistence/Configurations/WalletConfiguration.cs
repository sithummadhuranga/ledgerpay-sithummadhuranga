using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerPay.Infrastructure.Persistence.Configurations;

internal sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets", table =>
        {
            table.HasCheckConstraint("CK_Wallets_Balance_NonNegative", "[Balance] >= 0");
            table.HasCheckConstraint("CK_Wallets_Status_Valid", CheckSql.In<WalletStatus>("Status"));
            table.HasCheckConstraint(
                "CK_Wallets_WalletNumber_TwelveDigits",
                "LEN([WalletNumber]) = 12 AND [WalletNumber] NOT LIKE '%[^0-9]%'");
        });

        builder.HasKey(wallet => wallet.Id);
        builder.Property(wallet => wallet.WalletNumber).HasMaxLength(12).IsUnicode(false);
        builder.Property(wallet => wallet.Balance).HasPrecision(18, 2);
        builder.Property(wallet => wallet.Status).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
        builder.Property(wallet => wallet.StatusReason).HasMaxLength(250);
        builder.Property(wallet => wallet.StatusChangedAt).HasColumnType("datetime2");
        builder.Property(wallet => wallet.CreatedAt).HasColumnType("datetime2");

        // Backstop for the row locks: a write that lost a race fails instead of overwriting.
        builder.Property(wallet => wallet.RowVersion).IsRowVersion();

        builder.HasIndex(wallet => wallet.WalletNumber).IsUnique();
        builder.HasIndex(wallet => wallet.UserId).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(wallet => wallet.StatusChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
