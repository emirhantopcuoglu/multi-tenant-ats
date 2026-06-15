using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Ats.Modules.Jobs.Application;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter the JWT token (without 'Bearer' prefix)."
        };
        return Task.CompletedTask;
    });
});

// Order matters: the validation handler runs first and only handles
// ValidationException; everything else falls through to the catch-all.
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<TenantSaveChangesInterceptor>();
builder.Services.AddScoped<AuditableSaveChangesInterceptor>();

builder.Services.AddDbContext<TenantsDbContext>((sp, options) =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants"))
        .AddInterceptors(
            sp.GetRequiredService<TenantSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditableSaveChangesInterceptor>()));

builder.Services.AddDbContext<JobsDbContext>((sp, options) =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "jobs"))
        .AddInterceptors(
            sp.GetRequiredService<TenantSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditableSaveChangesInterceptor>()));

builder.Services.AddScoped<IJobsDbContext>(sp => sp.GetRequiredService<JobsDbContext>());
builder.Services.AddJobsApplication();

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<TenantsDbContext>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<InvitationOptions>(
    builder.Configuration.GetSection(InvitationOptions.SectionName));
builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
builder.Services.AddScoped<IInvitationService, InvitationService>();

// File storage (MinIO). The client is thread-safe and meant to be reused, so it is a
// singleton; MinioFileStorage is stateless and depends only on singletons, so it is too.
builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection(FileStorageOptions.SectionName));
builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value;
    return new MinioClient()
        .WithEndpoint(options.Endpoint)
        .WithCredentials(options.AccessKey, options.SecretKey)
        .WithSSL(options.UseSsl)
        .Build();
});
builder.Services.AddSingleton<IFileStorage, MinioFileStorage>();

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
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    await RoleSeeder.SeedAsync(roleManager);

    // Like the migrations and role seeding above, this couples startup to its backing
    // service being reachable — acceptable for a hard dependency in dev.
    var minioClient = scope.ServiceProvider.GetRequiredService<IMinioClient>();
    var fileStorageOptions = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>();
    await FileStorageInitializer.EnsureBucketAsync(minioClient, fileStorageOptions);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.AddPreferredSecuritySchemes("Bearer");
    });
}

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantClaimResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();
