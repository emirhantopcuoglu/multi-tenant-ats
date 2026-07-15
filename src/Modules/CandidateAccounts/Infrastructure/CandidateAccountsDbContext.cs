using Ats.Modules.CandidateAccounts.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Persistence for the global candidate accounts. Deliberately the simplest context in the system:
// CandidateAccount is not tenant-scoped, so — unlike every other module's context — there is no
// ICurrentTenant dependency and no global query filter. That is the whole point of a marketplace
// account: it spans every tenant instead of belonging to one.
public sealed class CandidateAccountsDbContext : DbContext
{
    public CandidateAccountsDbContext(DbContextOptions<CandidateAccountsDbContext> options)
        : base(options)
    {
    }

    public DbSet<CandidateAccount> CandidateAccounts => Set<CandidateAccount>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("candidate_accounts");

        builder.Entity<CandidateAccount>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Email).IsRequired().HasMaxLength(256);
            entity.Property(c => c.PasswordHash).IsRequired();
            // The DB default only matters once: it backfills accounts that existed before the column
            // did, giving each a unique stamp. New rows always arrive with a value from Register().
            entity.Property(c => c.SecurityStamp).IsRequired().HasDefaultValueSql("gen_random_uuid()");
            entity.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(c => c.LastName).IsRequired().HasMaxLength(100);
            // 16 = E.164's 15-digit maximum plus the leading '+'; the domain normalizes before storing.
            entity.Property(c => c.PhoneNumber).HasMaxLength(16);
            entity.Property(c => c.Country).HasMaxLength(100);
            entity.Property(c => c.City).HasMaxLength(100);
            entity.Property(c => c.CvFileKey).HasMaxLength(512);
            // One account per email across the whole marketplace. Enforced at the database, the only
            // place that holds under concurrent registrations.
            entity.HasIndex(c => c.Email).IsUnique();
        });
    }
}
