using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.Tenants.Infrastructure;

// Separate provider so email confirmation links last 24 hours, while the shared token lifespan
// stays at the 60-minute password-reset window.
public sealed class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    // Same as the candidate side's EmailVerificationRequest.ValidHours.
    public const int ValidHours = 24;

    public EmailConfirmationTokenProviderOptions()
    {
        Name = ProviderName;
        TokenLifespan = TimeSpan.FromHours(ValidHours);
    }

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
