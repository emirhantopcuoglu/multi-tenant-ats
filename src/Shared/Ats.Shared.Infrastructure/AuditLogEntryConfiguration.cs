using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ats.Shared.Infrastructure;

// Applied by every audited DbContext, so each module keeps its audit rows in its own schema.
// A constraint here can now fail the change itself, so only values the code controls get a length.
public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> entity)
    {
        entity.ToTable("AuditLogs");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Action).IsRequired().HasMaxLength(20);
        entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(100);
        entity.Property(e => e.ResourceId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.UserAgent).HasMaxLength(AuditLogEntry.UserAgentMaxLength);
        entity.Property(e => e.Before).HasColumnType("jsonb");
        entity.Property(e => e.After).HasColumnType("jsonb");

        // A tenant's changes by time, and one record's changes by time.
        entity.HasIndex(e => new { e.TenantId, e.OccurredAtUtc })
            .IsDescending(false, true);
        entity.HasIndex(e => new { e.TenantId, e.ResourceType, e.ResourceId, e.OccurredAtUtc })
            .IsDescending(false, false, false, true);
    }
}
