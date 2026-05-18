using MediMind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediMind.Infrastructure.Data.Configurations;

public class PlatformConfigurationConfiguration : IEntityTypeConfiguration<PlatformConfiguration>
{
    public void Configure(EntityTypeBuilder<PlatformConfiguration> builder)
    {
        builder.ToTable("platform_configurations");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TrialFeeEtb).HasColumnType("numeric(18,2)");
        builder.Property(c => c.BasicFeeEtb).HasColumnType("numeric(18,2)");
        builder.Property(c => c.PremiumFeeEtb).HasColumnType("numeric(18,2)");
        builder.Property(c => c.MaxAdvanceBookingDays).IsRequired();
        builder.Property(c => c.MaxSlotDurationMinutes).IsRequired();
        builder.Property(c => c.FeatureFlagsJson).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.MaintenanceMode).IsRequired();
        builder.Property(c => c.MaintenanceMessage).HasMaxLength(500);
    }
}
