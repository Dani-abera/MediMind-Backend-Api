using MediMind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediMind.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("log_id");
        builder.Property(l => l.Timestamp).IsRequired();
        builder.Property(l => l.UserId);
        builder.Property(l => l.UserType).HasMaxLength(50).IsRequired();
        builder.Property(l => l.CenterId);
        builder.Property(l => l.Action).HasMaxLength(100).IsRequired();
        builder.Property(l => l.EntityType).HasMaxLength(100);
        builder.Property(l => l.EntityId);
        builder.Property(l => l.IpAddress).HasMaxLength(50).IsRequired();
        builder.Property(l => l.UserAgent).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Metadata).HasColumnType("text");

        builder.HasIndex(l => l.Timestamp);
        builder.HasIndex(l => new { l.CenterId, l.Timestamp });
        builder.HasIndex(l => new { l.UserId, l.Timestamp });
        builder.HasIndex(l => l.Action);
    }
}
