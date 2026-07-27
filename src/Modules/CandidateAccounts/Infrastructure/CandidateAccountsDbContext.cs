using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
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
    public DbSet<EmailChangeRequest> EmailChangeRequests => Set<EmailChangeRequest>();
    public DbSet<CandidateRefreshToken> CandidateRefreshTokens => Set<CandidateRefreshToken>();
    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();
    public DbSet<EmailVerificationRequest> EmailVerificationRequests => Set<EmailVerificationRequest>();

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
            // The candidate's own file name, already stripped to safe characters before it reaches
            // storage; 255 is the ceiling every mainstream filesystem imposes on one anyway.
            entity.Property(c => c.CvFileName).HasMaxLength(255);
            // Two-letter code ("en", "tr"); the boundary normalizes anything longer down to it. The
            // DB default backfills accounts that predate the column — they registered when the app
            // only wrote English, so English is the honest value for them.
            entity.Property(c => c.PreferredLanguage)
                .IsRequired()
                .HasMaxLength(2)
                .HasDefaultValue(SupportedLanguages.Default);
            // Stored as the string name (project convention across modules); the default backfills
            // rows that existed before the column did — new rows are always born Active in code.
            entity.Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(CandidateAccountStatus.Active);
            // One account per email across the whole marketplace. Enforced at the database, the only
            // place that holds under concurrent registrations.
            entity.HasIndex(c => c.Email).IsUnique();
            // Soft-deleted accounts vanish from every query — login, security-stamp check, the
            // cross-module reader — without each call site remembering to filter. The one deliberate
            // bypass is IgnoreQueryFilters(), which nothing uses today.
            entity.HasQueryFilter(c => c.Status != CandidateAccountStatus.Deleted);
        });

        builder.Entity<EmailChangeRequest>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.NewEmail).IsRequired().HasMaxLength(256);
            // 44 = the exact length of a base64-encoded SHA-256 digest; anything longer is a bug.
            entity.Property(r => r.TokenHash).IsRequired().HasMaxLength(44);
            // Confirmation looks the request up by the hash of the presented token, so this is the
            // hot path; unique because two requests sharing a token would make "which one did the
            // click prove?" ambiguous.
            entity.HasIndex(r => r.TokenHash).IsUnique();
            // Deleting an account must take its pending requests with it — an orphaned request could
            // otherwise rename a recycled account id later.
            entity.HasOne<CandidateAccount>()
                .WithMany()
                .HasForeignKey(r => r.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CandidateRefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            // 44 = the exact length of a base64-encoded SHA-256 digest, same as EmailChangeRequest's.
            entity.Property(t => t.TokenHash).IsRequired().HasMaxLength(44);
            // Refresh looks the row up by the hash of the presented token, so this is the hot path.
            // Unique because two rows sharing a hash would make "which session is this?" ambiguous.
            entity.HasIndex(t => t.TokenHash).IsUnique();
            // Revoking a candidate's whole session set (and the logout-everywhere case) scans by
            // account, so the foreign key gets its own index rather than relying on the unique one.
            entity.HasIndex(t => t.CandidateAccountId);
            // A deleted account must take its refresh tokens with it: the row is anonymized rather
            // than removed, but an orphaned token could otherwise outlive the identity it names.
            entity.HasOne<CandidateAccount>()
                .WithMany()
                .HasForeignKey(t => t.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmailVerificationRequest>(entity =>
        {
            entity.HasKey(r => r.Id);
            // 44 = the exact length of a base64-encoded SHA-256 digest, same as the rows above.
            entity.Property(r => r.TokenHash).IsRequired().HasMaxLength(44);
            // Confirmation looks the row up by the hash of the presented token, so this is the hot
            // path. Unique for the same reason as EmailChangeRequest's: two requests sharing a token
            // would make "which one did the click prove?" ambiguous.
            entity.HasIndex(r => r.TokenHash).IsUnique();
            // Superseding older pending requests scans by account, so the foreign key is indexed.
            entity.HasIndex(r => r.CandidateAccountId);
            entity.HasOne<CandidateAccount>()
                .WithMany()
                .HasForeignKey(r => r.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PasswordResetRequest>(entity =>
        {
            entity.HasKey(r => r.Id);
            // 44 = the exact length of a base64-encoded SHA-256 digest, same as the two rows above.
            entity.Property(r => r.TokenHash).IsRequired().HasMaxLength(44);
            // Reset looks the row up by the hash of the presented token, so this is the hot path.
            // Unique because two requests sharing a token would make "which one did the click prove?"
            // ambiguous — the same reasoning as EmailChangeRequest's index.
            entity.HasIndex(r => r.TokenHash).IsUnique();
            // Superseding older pending requests scans by account, so the foreign key is indexed.
            entity.HasIndex(r => r.CandidateAccountId);
            entity.HasOne<CandidateAccount>()
                .WithMany()
                .HasForeignKey(r => r.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
