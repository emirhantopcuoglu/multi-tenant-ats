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
    // Identity, the candidate auth stack, the shared JWT scheme and the policies. One method because
    // they depend on each other.
    public static IHostApplicationBuilder AddIdentityAndAuthorization(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<JwtOptions>(
            builder.Configuration.GetSection(JwtOptions.SectionName));

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TenantsDbContext>()
            // Needed by GeneratePasswordResetTokenAsync. The token embeds the security stamp, so it is single-use.
            .AddDefaultTokenProviders()
            // Own provider so confirmation links outlive the 60-minute reset window.
            .AddTokenProvider<EmailConfirmationTokenProvider<ApplicationUser>>(
                EmailConfirmationTokenProviderOptions.ProviderName);

        // Lockout counters live on the account, so they also stop distributed attacks and still work when
        // Redis is down.
        builder.Services.Configure<LoginLockoutOptions>(
            builder.Configuration.GetSection(LoginLockoutOptions.SectionName));
        var loginLockoutOptions = builder.Configuration
            .GetSection(LoginLockoutOptions.SectionName).Get<LoginLockoutOptions>() ?? new LoginLockoutOptions();

        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.Tokens.EmailConfirmationTokenProvider = EmailConfirmationTokenProviderOptions.ProviderName;

            // Identity enforces these through SignInManager, which isn't used; AuthService calls the lockout
            // methods itself.
            options.Lockout.MaxFailedAccessAttempts = loginLockoutOptions.MaxFailedAttempts;
            options.Lockout.DefaultLockoutTimeSpan = loginLockoutOptions.LockoutDuration;
            // Otherwise new users have lockout disabled and the counters do nothing.
            options.Lockout.AllowedForNewUsers = true;
        });

        // Down from Identity's 24-hour default to match the candidate side.
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
        builder.Services.AddScoped<IUserManagementService, UserManagementService>();

        builder.Services.AddScoped<ITenantDirectory, TenantDirectory>();

        // Same "Jwt" section as the company side, so both token types share one key and scheme.
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
        // Shared by auth and profile so a password change issues a session the same way login does.
        builder.Services.AddScoped<ICandidateSessionIssuer, CandidateSessionIssuer>();
        builder.Services.AddScoped<ICandidateAuthService, CandidateAuthService>();
        builder.Services.AddScoped<ICandidateProfileService, CandidateProfileService>();
        builder.Services.AddScoped<ICandidatePasswordResetService, CandidatePasswordResetService>();
        // Registration uses it, so it is registered before ICandidateAuthService.
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

        // Controllers only know policy names; this is the one place that maps them to roles.
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.CanManageJobs, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter));

            options.AddPolicy(Policies.CanViewJobs, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter, Roles.HiringManager, Roles.ReadOnly));

            options.AddPolicy(Policies.CanManageUsers, policy =>
                policy.RequireRole(Roles.Admin));

            options.AddPolicy(Policies.CanManageTenant, policy =>
                policy.RequireRole(Roles.Admin));

            options.AddPolicy(Policies.CanViewApplications, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter, Roles.HiringManager, Roles.ReadOnly));

            options.AddPolicy(Policies.CanManageApplications, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter));

            // Hiring managers run interviews, so they can manage them; applications are Admin and Recruiter only.
            options.AddPolicy(Policies.CanViewInterviews, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter, Roles.HiringManager, Roles.ReadOnly));

            options.AddPolicy(Policies.CanManageInterviews, policy =>
                policy.RequireRole(Roles.Admin, Roles.Recruiter, Roles.HiringManager));

            // Resource-based: checked through IAuthorizationService.
            options.AddPolicy(Policies.IsInterviewParticipant, policy =>
                policy.AddRequirements(new InterviewerRequirement()));

            // Company tokens pass JWT validation but have no token_type=candidate claim, so they fail here.
            // The stamp requirement rejects tokens issued before the last password or email change.
            options.AddPolicy(Policies.CandidateOnly, policy =>
                policy.RequireClaim(TokenTypes.ClaimName, TokenTypes.Candidate)
                    .AddRequirements(new CandidateSecurityStampRequirement()));
        });

        builder.Services.AddSingleton<IAuthorizationHandler, InterviewerAuthorizationHandler>();

        // Scoped: it reads the stamp through the scoped DbContext.
        builder.Services.AddScoped<IAuthorizationHandler, CandidateSecurityStampHandler>();

        return builder;
    }
}
