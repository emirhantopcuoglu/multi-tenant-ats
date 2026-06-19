using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Infrastructure;
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
using MongoDB.Driver;
using RedisRateLimiting;
using Scalar.AspNetCore;
using StackExchange.Redis;

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

builder.Services.AddDbContext<ApplicationsDbContext>((sp, options) =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "applications"))
        .AddInterceptors(
            sp.GetRequiredService<TenantSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditableSaveChangesInterceptor>()));

builder.Services.AddScoped<IApplicationsDbContext>(sp => sp.GetRequiredService<ApplicationsDbContext>());
builder.Services.AddApplicationsApplication();

// MongoDB holds the append-only activity log (Sprint 4). The driver's MongoClient is thread-safe
// and pools connections internally, so it is a singleton; the database handle is derived from it.
// The repository is scoped because it depends on the per-request ICurrentTenant for isolation.
builder.Services.Configure<MongoOptions>(
    builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
    return new MongoClient(options.ConnectionString);
});
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
    return sp.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName);
});
builder.Services.AddScoped<IActivityLogRepository, MongoActivityLogRepository>();

// One Redis connection shared by the whole app. StackExchange.Redis multiplexes all traffic over a
// single ConnectionMultiplexer by design, so both the distributed cache (4.3) and the rate limiter
// (4.4) use this one instance. AbortOnConnectFail = false keeps the cache's fail-open behavior: a
// momentarily unreachable Redis does not crash startup, and the multiplexer reconnects on its own.
var redisOptions = builder.Configuration
    .GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
var redisConfiguration = ConfigurationOptions.Parse(redisOptions.ConnectionString);
redisConfiguration.AbortOnConnectFail = false;
// While Redis is unreachable, fail commands immediately instead of queueing them until the (5s)
// timeout. Both consumers here are fail-open (cache falls back to the database, the rate limiter lets
// the request through), so a fast failure keeps requests responsive during an outage instead of
// stacking multi-second waits per Redis call.
redisConfiguration.BacklogPolicy = BacklogPolicy.FailFast;
var redisConnection = ConnectionMultiplexer.Connect(redisConfiguration);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);

// Redis-backed distributed cache (Sprint 4.3). Caches the hot, effectively immutable slug -> tenantId
// lookup; TenantResolutionMiddleware treats it as best-effort, so a Redis outage degrades to a
// database read rather than failing the request.
builder.Services.AddStackExchangeRedisCache(options =>
    options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(redisConnection));

// Distributed rate limiting (Sprint 4.4). Counters live in Redis (via the shared multiplexer), so the
// limits hold across every app instance rather than per-process. Three fixed-window limits:
//   - per-IP   (named policy) on login/register/public apply — unauthenticated abuse vectors
//   - per-tenant + per-user (global, chained) on every authenticated request
// The native middleware's default rejection is 503; OnRejected corrects it to 429 + Retry-After.
var rateLimitingOptions = builder.Configuration
    .GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
var rateLimitWindow = TimeSpan.FromSeconds(rateLimitingOptions.WindowSeconds);

builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        // The Redis limiter reports retry-after in seconds under its own metadata name, not the
        // framework's MetadataName.RetryAfter (which the built-in in-memory limiters use).
        if (context.Lease.TryGetMetadata(RateLimitMetadataName.RetryAfter, out var retryAfterSeconds))
            context.HttpContext.Response.Headers.RetryAfter =
                retryAfterSeconds.ToString(NumberFormatInfo.InvariantInfo);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiting")
            .LogWarning("Rate limit exceeded for {Path}", context.HttpContext.Request.Path);

        await context.HttpContext.Response.WriteAsync(
            "Too many requests. Please try again later.", cancellationToken);
    };

    // Behind a reverse proxy (Sprint 8) the real client IP arrives in X-Forwarded-For, which requires
    // ForwardedHeaders middleware to populate RemoteIpAddress. In dev it is correct as-is.
    options.AddPolicy(RateLimitPolicies.PerIp, httpContext =>
        FailOpenRedisFixedWindow(
            httpContext,
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            rateLimitingOptions.PerIpPermitLimit));

    // CreateChained runs both limiters in sequence, so an authenticated request must satisfy its
    // tenant's shared budget and its own per-user budget. Unauthenticated requests carry neither
    // claim and fall through to NoLimiter here, relying on the per-IP policy instead.
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var tenantId = httpContext.User.FindFirstValue("tenant_id");
            return string.IsNullOrEmpty(tenantId)
                ? RateLimitPartition.GetNoLimiter("unauthenticated")
                : FailOpenRedisFixedWindow(httpContext, $"tenant:{tenantId}", rateLimitingOptions.PerTenantPermitLimit);
        }),
        PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrEmpty(userId)
                ? RateLimitPartition.GetNoLimiter("unauthenticated")
                : FailOpenRedisFixedWindow(httpContext, $"user:{userId}", rateLimitingOptions.PerUserPermitLimit);
        }));
});

// Builds a Redis-backed fixed-window partition wrapped in FailOpenRateLimiter, so a Redis outage lets
// the request through instead of failing it. The limiter for each key is created once and then cached
// by the partition, so resolving the logger per call here is cheap.
RateLimitPartition<string> FailOpenRedisFixedWindow(HttpContext httpContext, string key, int permitLimit)
{
    var logger = httpContext.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("RateLimiting");

    return RateLimitPartition.Get(key, partitionKey =>
        new FailOpenRateLimiter(
            new RedisFixedWindowRateLimiter<string>(partitionKey, new RedisFixedWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => redisConnection,
                PermitLimit = permitLimit,
                Window = rateLimitWindow
            }),
            logger,
            partitionKey));
}

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

    options.AddPolicy(Policies.CanViewApplications, policy =>
        policy.RequireRole(Roles.Admin, Roles.Recruiter, Roles.HiringManager, Roles.ReadOnly));

    options.AddPolicy(Policies.CanManageApplications, policy =>
        policy.RequireRole(Roles.Admin, Roles.Recruiter));
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

    // Ensure the activity-log read index exists. Idempotent, like the steps above.
    var mongoDatabase = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    await MongoActivityLogInitializer.EnsureIndexesAsync(mongoDatabase);
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
// After UseAuthentication so the tenant_id/sub claims the global limiter partitions on are populated,
// and after routing (added automatically at the pipeline start) so the per-IP policy can read the
// endpoint's [EnableRateLimiting] metadata.
app.UseRateLimiter();
app.UseMiddleware<TenantClaimResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();
