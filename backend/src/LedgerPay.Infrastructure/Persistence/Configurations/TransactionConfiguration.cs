using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerPay.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions", table =>
        {
            table.HasCheckConstraint("CK_Transactions_Amount_Positive", "[Amount] > 0");
            table.HasCheckConstraint("CK_Transactions_Fee_NonNegative", "[Fee] >= 0");
            table.HasCheckConstraint("CK_Transactions_Type_Valid", CheckSql.In<TransactionType>("Type"));
            table.HasCheckConstraint("CK_Transactions_Status_Valid", CheckSql.In<TransactionStatus>("Status"));
            table.HasCheckConstraint(
                "CK_Transactions_FailureCode_OnlyWhenFailed",
                "([Status] = 'Failed' AND [FailureCode] IS NOT NULL) OR ([Status] = 'Completed' AND [FailureCode] IS NULL)");
        });

        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Reference).HasMaxLength(24).IsUnicode(false);
        builder.Property(transaction => transaction.Type).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
        builder.Property(transaction => transaction.Status).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
        builder.Property(transaction => transaction.FailureCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(transaction => transaction.RequestedReceiver).HasMaxLength(50);
        builder.Property(transaction => transaction.Amount).HasPrecision(18, 2);
        builder.Property(transaction => transaction.Fee).HasPrecision(18, 2);
        builder.Property(transaction => transaction.Note).HasMaxLength(140);
        builder.Property(transaction => transaction.BankReference).HasMaxLength(40).IsUnicode(false);
        builder.Property(transaction => transaction.CreatedAt).HasColumnType("datetime2");

        builder.HasIndex(transaction => transaction.Reference).IsUnique();

        // A failed top-up may repeat a bank reference, so only completed top-ups must be unique.
        builder.HasIndex(transaction => transaction.BankReference)
            .IsUnique()
            .HasFilter("[BankReference] IS NOT NULL AND [Status] = 'Completed'");

        builder.HasIndex(transaction => new { transaction.SenderWalletId, transaction.CreatedAt })
            .IsDescending(false, true);
        builder.HasIndex(transaction => new { transaction.ReceiverWalletId, transaction.CreatedAt })
            .IsDescending(false, true);

        builder.HasOne(transaction => transaction.SenderWallet)
            .WithMany()
            .HasForeignKey(transaction => transaction.SenderWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transaction => transaction.ReceiverWallet)
            .WithMany()
            .HasForeignKey(transaction => transaction.ReceiverWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(transaction => transaction.InitiatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
