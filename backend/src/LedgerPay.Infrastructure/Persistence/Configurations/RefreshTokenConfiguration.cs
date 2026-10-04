using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerPay.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(token => token.CreatedAt).HasColumnType("datetime2");
        builder.Property(token => token.SessionStartedAt).HasColumnType("datetime2");
        builder.Property(token => token.ExpiresAt).HasColumnType("datetime2");
        builder.Property(token => token.RevokedAt).HasColumnType("datetime2");
        builder.Property(token => token.IpAddress).HasMaxLength(45).IsUnicode(false);
        builder.Property(token => token.UserAgent).HasMaxLength(200);

        // Every refresh finds its row by the hash of the token that came in.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // The sessions page and a revoke look up the rows of one session or of one user.
        builder.HasIndex(token => new { token.UserId, token.FamilyId });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
