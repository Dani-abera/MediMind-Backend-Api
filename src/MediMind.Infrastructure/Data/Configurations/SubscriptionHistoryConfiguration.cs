using MediMind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediMind.Infrastructure.Data.Configurations;

public class SubscriptionHistoryConfiguration : IEntityTypeConfiguration<SubscriptionHistory>
{
    public void Configure(EntityTypeBuilder<SubscriptionHistory> builder)
    {
        builder.ToTable("subscription_histories");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.CenterId).IsRequired();
        builder.Property(h => h.OldStatus).IsRequired().HasConversion<string>();
        builder.Property(h => h.NewStatus).IsRequired().HasConversion<string>();
        builder.Property(h => h.Plan).HasMaxLength(100);
        builder.Property(h => h.Notes).HasMaxLength(500);
        builder.Property(h => h.ChangedBy).IsRequired();
        builder.Property(h => h.ChangedAt).IsRequired();

        builder.HasOne(h => h.Center)
            .WithMany(c => c.SubscriptionHistories)
            .HasForeignKey(h => h.CenterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => h.CenterId);
        builder.HasIndex(h => h.ChangedAt);
    }
}
