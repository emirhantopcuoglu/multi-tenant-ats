using System.Text;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Interviews.Api.Authorization;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Tenants;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Ats.Api.Extensions;

public static class IdentityAndAuthorizationExtensions
{
    // Everything that decides who a request is and what it may do: ASP.NET Core Identity (company
    // side), the candidate marketplace's parallel auth stack, the JWT bearer scheme both share, and
    // the authorization policies/handlers controllers reference. Kept as one method because these
    // pieces are not independent — the JWT scheme validates tokens both identities issue, and the
    // CandidateOnly policy below depends on the candidate security-stamp handler registered here.
    public static IHostApplicationBuilder AddIdentityAndAuthorization(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<JwtOptions>(
            builder.Configuration.GetSection(JwtOptions.SectionName));

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TenantsDbContext>()
            // Required for GeneratePasswordResetTokenAsync: without a registered "Default" token provider that
            // call throws at runtime. The token is data-protection based (nothing stored) and embeds the user's
            // security stamp, so resetting the password invalidates it — that is what makes it single-use.
            .AddDefaultTokenProviders()
            // Email confirmation gets its own provider so it can outlive the password-reset window configured
            // below. DataProtectionTokenProviderOptions is a single global setting, and 60 minutes — correct for
            // a token that is a full account takeover while it lives — would make the only route into a
            // brand-new workspace expire while the founder is in a meeting. See EmailConfirmationTokenProvider.
            .AddTokenProvider<EmailConfirmationTokenProvider<ApplicationUser>>(
                EmailConfirmationTokenProviderOptions.ProviderName);

        // Brute-force protection for both login forms. The per-IP rate limiter counts requests from an
        // address, so it cannot see a distributed attack on one account — and FailOpenRateLimiter lets
        // everything through when Redis is down, which would otherwise leave the login form unguarded. These
        // counters live on the account instead. Shared with the candidate side so the two cannot drift.
        builder.Services.Configure<LoginLockoutOptions>(
            builder.Configuration.GetSection(LoginLockoutOptions.SectionName));
        var loginLockoutOptions = builder.Configuration
            .GetSection(LoginLockoutOptions.SectionName).Get<LoginLockoutOptions>() ?? new LoginLockoutOptions();

        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.Tokens.EmailConfirmationTokenProvider = EmailConfirmationTokenProviderOptions.ProviderName;

            // Identity owns the counters (AccessFailedCount/LockoutEnd) but enforces them through
            // SignInManager, which this codebase does not use — AuthService calls CheckPasswordAsync
            // directly, so it also calls AccessFailedAsync/IsLockedOutAsync itself. These options are what
            // those calls read.
            options.Lockout.MaxFailedAccessAttempts = loginLockoutOptions.MaxFailedAttempts;
            options.Lockout.DefaultLockoutTimeSpan = loginLockoutOptions.LockoutDuration;
            // Without this a newly created user has LockoutEnabled = false and IsLockedOutAsync always
            // answers false, so the counters would tick up against nothing.
            options.Lockout.AllowedForNewUsers = true;
        });

        // Tighten Identity's token lifespan from its 24-hour default. A reset token is a full account takeover
        // for as long as it lives, and an hour covers "open inbox, click link" — the same window the candidate
        // side's PasswordResetRequest uses. Read from the PasswordReset section so both agree on one number.
        var passwordResetOptions = builder.Configuration
            .GetSection(PasswordResetOptions.SectionName).Get<PasswordResetOptions>() ?? new PasswordResetOptions();

        builder.Services.Configure<PasswordResetOptions>(
            builder.Configuration.GetSection(PasswordResetOptions.SectionName));
        builder.Services.Configure<EmailConfirmationOptions>(
            builder.Configuration.GetSection(EmailConfirmationOptions.SectionName));
        builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromMinutes(passwordResetOptions.ValidMinutes));

        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<ITenantProfileService, TenantProfileService>();
        // Administering people in a tenant, kept off IAuthService: that interface backs the anonymous login
        // endpoints, and "change someone else's role" does not belong on the same surface.
        builder.Services.AddScoped<IUserManagementService, UserManagementService>();

        // The Tenants module's cross-module read port, consumed (e.g.) by the Jobs public feed to name the
        // company behind each job without reaching into the Tenants schema.
        builder.Services.AddScoped<ITenantDirectory, TenantDirectory>();

        // Candidate authentication (FAZ 7). Binds the same "Jwt" section as the company side, so candidate and
        // company tokens share one signing key and are validated by the one JWT bearer scheme; they are told
        // apart only by the token_type claim. The password hasher is Identity's PBKDF2 hasher (stateless, so a
        // singleton) adapted to a subject-less port.
        builder.Services.Configure<CandidateJwtOptions>(
            builder.Configuration.GetSection(CandidateJwtOptions.SectionName));
        builder.Services.Configure<CandidateEmailChangeOptions>(
            builder.Configuration.GetSection(CandidateEmailChangeOptions.SectionName));
        builder.Services.Configure<CandidatePasswordResetOptions>(
            builder.Configuration.GetSection(CandidatePasswordResetOptions.SectionName));
        builder.Services.Configure<CandidateEmailVerificationOptions>(
            builder.Configuration.GetSection(CandidateEmailVerificationOptions.SectionName));
        builder.Services.Configure<InterviewRoomOptions>(
            builder.Configuration.GetSection(InterviewRoomOptions.SectionName));
        builder.Services.AddSingleton<IPasswordHasher<CandidateAccount>, PasswordHasher<CandidateAccount>>();
        builder.Services.AddScoped<ICandidatePasswordHasher, CandidatePasswordHasher>();
        builder.Services.AddScoped<ICandidateTokenService, CandidateTokenService>();
        // Single owner of candidate session minting/storage, shared by the auth and profile services so a
        // password change re-issues a session exactly the way login does.
        builder.Services.AddScoped<ICandidateSessionIssuer, CandidateSessionIssuer>();
        builder.Services.AddScoped<ICandidateAuthService, CandidateAuthService>();
        builder.Services.AddScoped<ICandidateProfileService, CandidateProfileService>();
        // Kept apart from the profile service: that one serves a signed-in candidate re-proving ownership with
        // their current password, this one serves someone who cannot sign in and proves it via their mailbox.
        builder.Services.AddScoped<ICandidatePasswordResetService, CandidatePasswordResetService>();
        // Proving the address on the account, as opposed to the two services above which change credentials.
        // Registration calls it, so it must be registered before ICandidateAuthService can resolve.
        builder.Services.AddScoped<ICandidateEmailVerificationService, CandidateEmailVerificationService>();
        builder.Services.AddScoped<ICandidateAccountLifecycleService, CandidateAccountLifecycleService>();
        builder.Services.AddScoped<ICandidateAccountReader, CandidateAccountReader>();

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret))
                };
            });

        // Policies map a capability to the roles that satisfy it. Controllers reference
        // only the policy name (Policies.*); this composition root is the single place
        // that knows which concrete roles (Roles.*) each capability requires. Management
        // roles are a subset of the viewing roles by design.
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.CanManageJobs, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter));

            options.AddPolicy(Policies.CanViewJobs, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter, Roles.HiringManager, Roles.ReadOnly));

            options.AddPolicy(Policies.CanManageUsers, policy =>
                policy.RequireRole(Roles.Admin));

            // Company-wide presentation (the public profile). Same trust level as managing users today,
            // but a distinct policy: the two capabilities have no inherent reason to stay coupled.
            options.AddPolicy(Policies.CanManageTenant, policy =>
                policy.RequireRole(Roles.Admin));

            options.AddPolicy(Policies.CanViewApplications, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter, Roles.HiringManager, Roles.ReadOnly));

            options.AddPolicy(Policies.CanManageApplications, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter));

            // Hiring managers run interviews, so they can both view and schedule them — unlike applications,
            // where managing is limited to Admin and Recruiter.
            options.AddPolicy(Policies.CanViewInterviews, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter, Roles.HiringManager, Roles.ReadOnly));

            options.AddPolicy(Policies.CanManageInterviews, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter, Roles.HiringManager));

            // Resource-based: checked imperatively via IAuthorizationService against a loaded InterviewDetailDto.
            options.AddPolicy(Policies.IsInterviewParticipant, policy =>
                policy.AddRequirements(new InterviewerRequirement()));

            // Candidate-only endpoints. Requires a candidate (marketplace) token: a company token is signed by
            // the same key and passes JWT validation, but carries no token_type=candidate claim, so it fails
            // here. The reverse — a candidate token on a company endpoint — is already blocked by those
            // endpoints' role requirements, which a role-less candidate token cannot meet.
            // The security-stamp requirement additionally rejects tokens issued before the account's last
            // password change (the stamp rotates on change), revoking stolen or stale sessions immediately.
            options.AddPolicy(Policies.CandidateOnly, policy =>
                policy.RequireClaim(TokenTypes.ClaimName, TokenTypes.Candidate)
                    .AddRequirements(new CandidateSecurityStampRequirement()));
        });

        // Stateless handler — singleton is safe and avoids allocating per-request.
        builder.Services.AddSingleton<IAuthorizationHandler, InterviewerAuthorizationHandler>();

        // Scoped, unlike the handler above: it reads the account's current security stamp through the
        // scoped CandidateAccountsDbContext on every authenticated candidate request.
        builder.Services.AddScoped<IAuthorizationHandler, CandidateSecurityStampHandler>();

        return builder;
    }
}
