using Ats.Modules.Notifications.Application;
using Ats.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Infrastructure;

// Persistence for in-app notifications. Like CandidateAccountsDbContext there is no ICurrentTenant
// and no global query filter: the scope root here is the recipient, not a tenant. Candidate
// recipients are global marketplace accounts whose feed spans every tenant, and company-user rows
// (later) carry their TenantId as data — every query in the module filters on
// (RecipientType, RecipientId) explicitly, which is the ownership boundary.
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
            // Enums as strings, matching the other modules: readable in the database and stable
            // when the enum gains members, at the cost of a few bytes per row.
            entity.Property(n => n.RecipientType).HasConversion<string>().HasMaxLength(20);
            entity.Property(n => n.Type).HasConversion<string>().HasMaxLength(50);
            // jsonb rather than text: Postgres validates the document on write, and the column
            // stays queryable server-side if a future feature needs to filter on payload fields.
            entity.Property(n => n.Payload).IsRequired().HasColumnType("jsonb");

            // The one access path this module has: a recipient's feed, newest first (the btree is
            // scanned backwards for the DESC order), and the unread badge count over the same
            // leading columns.
            entity.HasIndex(n => new { n.RecipientType, n.RecipientId, n.CreatedAtUtc });
        });
    }
}
