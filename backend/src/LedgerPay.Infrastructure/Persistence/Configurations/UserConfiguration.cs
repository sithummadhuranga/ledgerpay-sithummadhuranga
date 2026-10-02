using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerPay.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", table =>
        {
            // Binary collation, because the default one ignores case and would let an uppercase email through.
            table.HasCheckConstraint("CK_Users_Email_Lowercase", "[Email] COLLATE Latin1_General_BIN2 = LOWER([Email])");
            table.HasCheckConstraint("CK_Users_FailedLoginCount_NonNegative", "[FailedLoginCount] >= 0");
        });

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Email).HasMaxLength(254).IsUnicode(false);
        builder.Property(user => user.Phone).HasMaxLength(12).IsUnicode(false);
        builder.Property(user => user.FullName).HasMaxLength(100);
        builder.Property(user => user.PasswordHash).HasMaxLength(256).IsUnicode(false);
        builder.Property(user => user.LockoutEnd).HasColumnType("datetime2");
        builder.Property(user => user.CreatedAt).HasColumnType("datetime2");

        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasIndex(user => user.Phone).IsUnique();

        builder.HasOne(user => user.Wallet)
            .WithOne(wallet => wallet.User)
            .HasForeignKey<Wallet>(wallet => wallet.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
