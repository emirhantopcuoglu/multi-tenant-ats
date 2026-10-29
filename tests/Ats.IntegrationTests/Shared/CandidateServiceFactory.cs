using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ats.Shared.Infrastructure;

namespace Ats.IntegrationTests.Shared;

// Builds the real CandidateEmailVerificationService instead of a fake per suite. NoOpEmailSender by
// default; suites that check the link pass a RecordingEmailSender.
internal static class CandidateServiceFactory
{
    internal static CandidateEmailVerificationService EmailVerification(
        CandidateAccountsDbContext db, IEmailSender? emailSender = null) =>
        new(db,
            emailSender ?? new NoOpEmailSender(),
            new JsonEmailTextProvider(),
            Options.Create(new CandidateEmailVerificationOptions()),
            NullLogger<CandidateEmailVerificationService>.Instance);

    /* The production defaults, so a suite that is not about lockout behaves as production does.
       A test that IS about lockout passes its own smaller numbers. */
    internal static IOptions<LoginLockoutOptions> Lockout(
        int maxFailedAttempts = 5, int lockoutMinutes = 15) =>
        Options.Create(new LoginLockoutOptions
        {
            MaxFailedAttempts = maxFailedAttempts,
            LockoutMinutes = lockoutMinutes,
        });
}
