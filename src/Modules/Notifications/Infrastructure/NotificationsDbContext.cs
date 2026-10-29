using Ats.Modules.Notifications.Application;
using Ats.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Infrastructure;

// No tenant filter: the recipient is the scope. Every query filters on (RecipientType, RecipientId).
public sealed class NotificationsDbContext : DbContext, INotificationsDbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("notifications");

        builder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.RecipientType).HasConversion<string>().HasMaxLength(20);
            entity.Property(n => n.Type).HasConversion<string>().HasMaxLength(50);
            entity.Property(n => n.Payload).IsRequired().HasColumnType("jsonb");

            // Serves the feed (newest first) and the unread count.
            entity.HasIndex(n => new { n.RecipientType, n.RecipientId, n.CreatedAtUtc });
        });
    }
}
