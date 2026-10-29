using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Accounts are global, so unlike the other contexts there is no tenant filter.
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
            // The default only backfilled rows that existed before the column.
            entity.Property(c => c.SecurityStamp).IsRequired().HasDefaultValueSql("gen_random_uuid()");
            entity.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(c => c.LastName).IsRequired().HasMaxLength(100);
            // E.164: 15 digits plus '+'.
            entity.Property(c => c.PhoneNumber).HasMaxLength(16);
            entity.Property(c => c.Country).HasMaxLength(100);
            entity.Property(c => c.City).HasMaxLength(100);
            entity.Property(c => c.CvFileKey).HasMaxLength(512);
            entity.Property(c => c.CvFileName).HasMaxLength(255);
            // Older accounts registered when all mail was English, hence the default.
            entity.Property(c => c.PreferredLanguage)
                .IsRequired()
                .HasMaxLength(2)
                .HasDefaultValue(SupportedLanguages.Default);
            entity.Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(CandidateAccountStatus.Active);
            entity.HasIndex(c => c.Email).IsUnique();
            // Hides deleted accounts from login, the stamp check and the cross-module reader.
            entity.HasQueryFilter(c => c.Status != CandidateAccountStatus.Deleted);
        });

        builder.Entity<EmailChangeRequest>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.NewEmail).IsRequired().HasMaxLength(256);
            // Base64 SHA-256 is always 44 characters.
            entity.Property(r => r.TokenHash).IsRequired().HasMaxLength(44);
            entity.HasIndex(r => r.TokenHash).IsUnique();
            entity.HasOne<CandidateAccount>()
                .WithMany()
                .HasForeignKey(r => r.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CandidateRefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).IsRequired().HasMaxLength(44);
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.CandidateAccountId);
            entity.HasOne<CandidateAccount>()
                .WithMany()
                .HasForeignKey(t => t.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmailVerificationRequest>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.TokenHash).IsRequired().HasMaxLength(44);
            entity.HasIndex(r => r.TokenHash).IsUnique();
            entity.HasIndex(r => r.CandidateAccountId);
            entity.HasOne<CandidateAccount>()
                .WithMany()
                .HasForeignKey(r => r.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PasswordResetRequest>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.TokenHash).IsRequired().HasMaxLength(44);
            entity.HasIndex(r => r.TokenHash).IsUnique();
            entity.HasIndex(r => r.CandidateAccountId);
            entity.HasOne<CandidateAccount>()
                .WithMany()
                .HasForeignKey(r => r.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
