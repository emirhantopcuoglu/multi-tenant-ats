using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.Tenants.Infrastructure;

// Identity's DataProtectionTokenProviderOptions is a SINGLE global setting shared by every
// data-protection token it issues. Program.cs tightens it to the password-reset window (60 minutes),
// which is correct for a token that is a full account takeover while it lives — but wrong for an email
// confirmation link, which is the only way into a brand-new workspace. An hour would mean: register,
// get pulled into a meeting, come back, and the tenant you just created is unreachable.
//
// So email confirmation gets its own provider with its own lifespan. This is the shape Identity
// documents for a per-purpose lifespan: subclass the options to carry a distinct name and duration,
// subclass the provider so DI can resolve those options for it specifically.
public sealed class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    // Matches the candidate side's EmailVerificationRequest.ValidHours. Both answer the same question
    // — "can this person read this mailbox?" — and both have the same realistic failure mode: someone
    // who closes the tab and comes back that evening.
    public const int ValidHours = 24;

    public EmailConfirmationTokenProviderOptions()
    {
        Name = ProviderName;
        TokenLifespan = TimeSpan.FromHours(ValidHours);
    }

    // The key Identity registers the provider under and that Tokens.EmailConfirmationTokenProvider
    // points at. A constant so the two cannot drift into a runtime "no such provider" failure.
    public const string ProviderName = "AtsEmailConfirmation";
}

public sealed class EmailConfirmationTokenProvider<TUser> : DataProtectorTokenProvider<TUser>
    where TUser : class
{
    public EmailConfirmationTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<EmailConfirmationTokenProviderOptions> options,
        ILogger<DataProtectorTokenProvider<TUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}
