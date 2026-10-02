using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerPay.Infrastructure.Persistence.Configurations;

internal sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings", table =>
        {
            table.HasCheckConstraint("CK_SystemSettings_Value_NonNegative", "[Value] >= 0");
        });

        builder.HasKey(setting => setting.Id);
        builder.Property(setting => setting.Key).HasMaxLength(100).IsUnicode(false);
        builder.Property(setting => setting.Value).HasPrecision(18, 2);
        builder.Property(setting => setting.UpdatedAt).HasColumnType("datetime2");
        builder.HasIndex(setting => setting.Key).IsUnique();
    }
}
