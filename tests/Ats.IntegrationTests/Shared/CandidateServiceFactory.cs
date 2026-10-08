using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ats.Shared.Infrastructure;

namespace Ats.IntegrationTests.Shared;

// Builds the real CandidateEmailVerificationService rather than a stub. Three suites construct
// CandidateAuthService, whose registration path now depends on it, and none of them care about
// verification — a hand-rolled fake in each would be three chances to drift from the real behaviour
// for no benefit. The default NoOpEmailSender keeps those suites silent; a suite that asserts on the
// mailed link passes its own RecordingEmailSender.
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
