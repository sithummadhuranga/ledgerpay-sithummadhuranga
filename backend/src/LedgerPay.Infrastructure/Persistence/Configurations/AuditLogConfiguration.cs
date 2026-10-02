using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerPay.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.CreatedAt).HasColumnType("datetime2");
        builder.Property(log => log.Action).HasMaxLength(50).IsUnicode(false);
        builder.Property(log => log.EntityType).HasMaxLength(50).IsUnicode(false);
        builder.Property(log => log.EntityReference).HasMaxLength(100).IsUnicode(false);
        builder.Property(log => log.IpAddress).HasMaxLength(45).IsUnicode(false);
        builder.Property(log => log.CorrelationId).HasMaxLength(64).IsUnicode(false);
        builder.Property(log => log.Details).HasMaxLength(1000);

        builder.HasIndex(log => log.CreatedAt);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(log => log.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
