using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Ats.Api;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using RabbitMQ.Client;
using Serilog;
using Serilog.Enrichers.OpenTelemetry;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using MassTransit;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.Interviews.Api.Authorization;
using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ats.Modules.Jobs.Application;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Tenants;
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

// Bootstrap Serilog before the host so startup errors (DB unreachable, bad config) are also
// captured. ReadFrom.Configuration picks up the "Serilog" section from appsettings;
// ReadFrom.Services allows DI-registered sinks (none currently, but keeps the door open).
// Destructure.With masks sensitive properties when {@obj} is used in log messages.
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, config) => config
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    // Adds TraceId and SpanId from the current OTel Activity to every log event, so a log line
    // in Seq can be linked to the matching trace in Jaeger by TraceId.
    .Enrich.WithOpenTelemetryTraceId()
    .Enrich.WithOpenTelemetrySpanId()
    .Destructure.With(new SensitiveDataMaskingPolicy()));

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
builder.Services.AddScoped<AuditLogInterceptor>();

builder.Services.AddDbContext<TenantsDbContext>((sp, options) =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants"))
        .AddInterceptors(
            sp.GetRequiredService<TenantSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditableSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditLogInterceptor>()));

builder.Services.AddDbContext<JobsDbContext>((sp, options) =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "jobs"))
        .AddInterceptors(
            sp.GetRequiredService<TenantSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditableSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditLogInterceptor>()));

builder.Services.AddScoped<IJobsDbContext>(sp => sp.GetRequiredService<JobsDbContext>());
builder.Services.AddJobsApplication();

builder.Services.AddDbContext<ApplicationsDbContext>((sp, options) =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "applications"))
        .AddInterceptors(
            sp.GetRequiredService<TenantSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditableSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditLogInterceptor>()));

builder.Services.AddScoped<IApplicationsDbContext>(sp => sp.GetRequiredService<ApplicationsDbContext>());
builder.Services.AddApplicationsApplication();

builder.Services.AddDbContext<InterviewsDbContext>((sp, options) =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "interviews"))
        .AddInterceptors(
            sp.GetRequiredService<TenantSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditableSaveChangesInterceptor>(),
            sp.GetRequiredService<AuditLogInterceptor>()));

builder.Services.AddScoped<IInterviewsDbContext>(sp => sp.GetRequiredService<InterviewsDbContext>());
builder.Services.AddInterviewsApplication();

// Candidate accounts (FAZ 7): the marketplace's global, tenant-less identity. Unlike every other
// context, this one takes no tenant/audit interceptors — its only entity is neither ITenantScoped nor
// IAuditable, so those interceptors would be inert. Registered here (deferred from 7.3) now that the
// auth services below consume it.
builder.Services.AddDbContext<CandidateAccountsDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "candidate_accounts")));

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

// CV parse results (Sprint 6.3) also live in MongoDB. Scoped like the activity log because the read
// path depends on the per-request ICurrentTenant; the write path (the CV-parsing consumer) passes the
// tenant explicitly, since it runs outside a resolved-tenant request.
builder.Services.AddScoped<ICvParseResultRepository, MongoCvParseResultRepository>();

// Candidate full-text search (Sprint 6.4). Backed by a PostgreSQL tsvector generated column on the
// Candidates table; the repository is scoped because the underlying DbContext is scoped.
builder.Services.AddScoped<ICandidateSearchRepository, CandidateSearchRepository>();

// LLM-backed CV parsing (Sprint 6.3). The PDF text extractor and the parser are stateless and
// thread-safe (the parser holds one reusable Polly pipeline and pulls HTTP clients from the factory),
// so both are singletons. The parser targets any OpenAI-compatible API; it defaults to GitHub Models
// (free with a GitHub token). The key is read from User Secrets / env via LlmOptions, never from
// appsettings.json.
builder.Services.Configure<LlmOptions>(
    builder.Configuration.GetSection(LlmOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
builder.Services.AddSingleton<ICvParser, OpenAiCompatibleCvParser>();

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

// CORS for the SPA (Sprint 8.1). The front-end (Ats.Web) runs on a different origin than the API, so
// the browser blocks its requests unless the API allows that origin. The allowed origins come from the
// "Cors" section so each environment lists its own front-end without a code change. AllowCredentials is
// required because the refresh flow carries credentials cross-origin; the CORS spec then forbids a
// wildcard origin, which is why AllowedOrigins is an explicit list. Retry-After is exposed so the SPA
// can read the rate limiter's back-off hint (cross-origin responses hide non-safelisted headers by default).
var corsOptions = builder.Configuration
    .GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicies.Spa, policy => policy
        .WithOrigins(corsOptions.AllowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("Retry-After")));

// Forwarded headers (Sprint 8.1). Behind a reverse proxy (nginx/Caddy in Sprint 8), the real client IP
// and scheme arrive in X-Forwarded-For / X-Forwarded-Proto; without this middleware RemoteIpAddress is
// the proxy's address. The per-IP rate limiter partitions on RemoteIpAddress, so it would otherwise
// throttle every client behind the proxy as one. KnownNetworks/KnownProxies are cleared because the
// proxy runs in an unknown Docker/host network range; this is safe only when the app is not exposed
// directly to the internet (it always sits behind the proxy) — revisit if that assumption changes.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// RabbitMQ message bus (Sprint 5). MassTransit is the abstraction over the broker: it owns the
// connection, retries, and (Sprint 5.3) the outbox, and lets consumer code stay transport-agnostic.
// Sprint 5.2 added the first consumer (application-submitted -> candidate email). Unlike the
// Mongo/MinIO initializers, MassTransit's hosted service connects in the background and retries on
// its own, so a broker that is briefly unreachable does not crash startup.
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddMassTransit(bus =>
{
    // Consumer endpoints get readable, kebab-cased queue names instead of namespaced defaults.
    bus.SetKebabCaseEndpointNameFormatter();

    // Transactional outbox. Publishing an integration event no longer hits the broker inline;
    // instead the message is written to the outbox tables in the applications schema as part of the
    // same SaveChanges as the business change. A background delivery service then forwards it to
    // RabbitMQ and marks it delivered. The result is atomicity (both the row and the message commit,
    // or neither) and durability (a broker outage delays delivery, never loses or blocks the request).
    bus.AddEntityFrameworkOutbox<ApplicationsDbContext>(outbox =>
    {
        outbox.UsePostgres();
        outbox.UseBusOutbox();
    });

    // Notifications consumers: email the candidate when an application is submitted, and again when
    // it is rejected. ConfigureEndpoints below creates and binds each consumer's queue automatically.
    bus.AddConsumer<ApplicationSubmittedConsumer>();
    bus.AddConsumer<ApplicationRejectedConsumer>();

    // CV-parsing consumer (Sprint 6.3): downloads the CV, extracts text, asks Claude for structured
    // data, and stores it in MongoDB. Inherits the retry/dead-letter policy configured below.
    bus.AddConsumer<CvParsingConsumer>();

    bus.UsingRabbitMq((context, configurator) =>
    {
        var rabbitMqOptions = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        configurator.Host(rabbitMqOptions.Host, rabbitMqOptions.Port, rabbitMqOptions.VirtualHost, host =>
        {
            host.Username(rabbitMqOptions.Username);
            host.Password(rabbitMqOptions.Password);
        });

        // Consumer retry policy (Sprint 5.5). Applied here, before ConfigureEndpoints, so every consumer
        // endpoint inherits it: a throwing consumer is retried with an exponential back-off instead of
        // failing once or looping forever. intervalDelta is set to the initial interval so the back-off
        // grows from there. When all attempts are exhausted, MassTransit moves the message to the
        // endpoint's "<queue>_error" dead-letter queue automatically — no extra wiring needed.
        configurator.UseMessageRetry(retry => retry.Exponential(
            retryLimit: rabbitMqOptions.RetryLimit,
            minInterval: TimeSpan.FromSeconds(rabbitMqOptions.RetryInitialIntervalSeconds),
            maxInterval: TimeSpan.FromSeconds(rabbitMqOptions.RetryMaxIntervalSeconds),
            intervalDelta: TimeSpan.FromSeconds(rabbitMqOptions.RetryInitialIntervalSeconds)));

        // Auto-wires every registered consumer to its endpoint and binds the retry policy above.
        configurator.ConfigureEndpoints(context);
    });
});

// Message idempotency guard (Sprint 5.5). RabbitMQ delivers at-least-once, so a consumer can see the
// same message twice (a lost ack, a retry, or an error-queue replay). The guard marks a processed
// message in Redis so a duplicate delivery does not send a duplicate email. It reuses the shared Redis
// multiplexer registered above; it is stateless, so a singleton is fine.
builder.Services.Configure<IdempotencyOptions>(
    builder.Configuration.GetSection(IdempotencyOptions.SectionName));
builder.Services.AddSingleton<IIdempotencyGuard, RedisIdempotencyGuard>();

// Hangfire background jobs (Sprint 5.4). Jobs are stored in PostgreSQL (Hangfire's own "hangfire" schema,
// created automatically and kept separate from our EF migrations) so they survive restarts, and run on a
// server hosted in this process. In a multi-instance deployment Hangfire's storage-level locks ensure a
// recurring job runs on only one instance at a time — the reason to use it over a raw BackgroundService.
builder.Services.Configure<HangfireOptions>(
    builder.Configuration.GetSection(HangfireOptions.SectionName));
builder.Services.AddHangfire(hangfire => hangfire
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(postgres =>
        postgres.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Postgres"))));
builder.Services.AddHangfireServer();

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<TenantsDbContext>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// The Tenants module's cross-module read port, consumed (e.g.) by the Jobs public feed to name the
// company behind each job without reaching into the Tenants schema.
builder.Services.AddScoped<ITenantDirectory, TenantDirectory>();

// Candidate authentication (FAZ 7). Binds the same "Jwt" section as the company side, so candidate and
// company tokens share one signing key and are validated by the one JWT bearer scheme; they are told
// apart only by the token_type claim. The password hasher is Identity's PBKDF2 hasher (stateless, so a
// singleton) adapted to a subject-less port.
builder.Services.Configure<CandidateJwtOptions>(
    builder.Configuration.GetSection(CandidateJwtOptions.SectionName));
builder.Services.AddSingleton<IPasswordHasher<CandidateAccount>, PasswordHasher<CandidateAccount>>();
builder.Services.AddScoped<ICandidatePasswordHasher, CandidatePasswordHasher>();
builder.Services.AddScoped<ICandidateTokenService, CandidateTokenService>();
builder.Services.AddScoped<ICandidateAuthService, CandidateAuthService>();
builder.Services.AddScoped<ICandidateAccountReader, CandidateAccountReader>();

builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<InvitationOptions>(
    builder.Configuration.GetSection(InvitationOptions.SectionName));
builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
builder.Services.AddScoped<IInvitationService, InvitationService>();

// Resolved per execution inside the Hangfire job scope; scheduled below after the app is built.
builder.Services.AddScoped<ExpiredInvitationCleanupJob>();

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
    options.AddPolicy(Policies.CandidateOnly, policy =>
        policy.RequireClaim(TokenTypes.ClaimName, TokenTypes.Candidate));
});

// Stateless handler — singleton is safe and avoids allocating per-request.
builder.Services.AddSingleton<IAuthorizationHandler, InterviewerAuthorizationHandler>();

// Distributed tracing (Sprint 7.2). A single TracerProvider covers all instrumented activities.
// The OTLP exporter forwards spans to Jaeger (http://localhost:4317 dev); swap the endpoint
// for Tempo, Honeycomb, or Datadog in other environments — code stays unchanged.
//
// Why OTLP instead of Jaeger's own exporter? OTLP is vendor-neutral: Jaeger, Grafana Tempo,
// and most hosted APM tools all accept it. The Jaeger-specific exporter is deprecated.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: builder.Configuration["OpenTelemetry:ServiceName"] ?? "ats-api",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            // Health check endpoints produce noise with no diagnostic value.
            options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        // MassTransit registers its own ActivitySource under "MassTransit". Subscribing here
        // creates spans for every publish, consume, and send — the RabbitMQ leg of a request
        // appears as a child span of the HTTP span that triggered the publish.
        .AddSource("MassTransit")
        // Redis instrumentation. Receives the shared multiplexer so it can subscribe to the
        // ProfiledCommand events that StackExchange.Redis emits per operation.
        .AddRedisInstrumentation(redisConnection)
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(
                builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317");
        }));

// Health checks for liveness and readiness probes.
//
// Why two endpoints instead of one?
// - /health/live answers "is the process alive?" — no external deps. If this fails, restart the pod.
// - /health/ready answers "can the process serve traffic?" — all deps must respond. If this fails,
//   remove the instance from the load balancer until it recovers.
//
// The RabbitMQ IConnection is registered as a long-lived singleton (per RabbitMQ's own guidelines)
// and resolved lazily so it does not block startup if the broker is temporarily unreachable.
var rabbitMqHealthOptions = builder.Configuration
    .GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();

var mongoHealthOptions = builder.Configuration
    .GetSection(MongoOptions.SectionName).Get<MongoOptions>() ?? new MongoOptions();

var rabbitMqHealthConnection = new Lazy<Task<IConnection>>(() =>
    new ConnectionFactory
    {
        HostName = rabbitMqHealthOptions.Host,
        Port = rabbitMqHealthOptions.Port,
        VirtualHost = rabbitMqHealthOptions.VirtualHost,
        UserName = rabbitMqHealthOptions.Username,
        Password = rabbitMqHealthOptions.Password
    }.CreateConnectionAsync());

builder.Services
    .AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("Postgres")!,
        name: "postgres",
        tags: ["ready"])
    .AddRedis(
        sp => sp.GetRequiredService<IConnectionMultiplexer>(),
        name: "redis",
        tags: ["ready"])
    .AddRabbitMQ(
        _ => rabbitMqHealthConnection.Value,
        name: "rabbitmq",
        tags: ["ready"])
    .AddMongoDb(
        clientFactory: sp => (MongoClient)sp.GetRequiredService<IMongoClient>(),
        databaseNameFactory: _ => mongoHealthOptions.DatabaseName,
        name: "mongodb",
        tags: ["ready"])
    .AddCheck<MinioHealthCheck>("minio", tags: ["ready"]);

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

    // Ensure the activity-log and audit-log read indexes exist. Idempotent.
    var mongoDatabase = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    await MongoActivityLogInitializer.EnsureIndexesAsync(mongoDatabase);
    await MongoAuditLogInitializer.EnsureIndexesAsync(mongoDatabase);
}

// Apply X-Forwarded-* first so every downstream component (request logging, metrics, the per-IP rate
// limiter) sees the real client IP and scheme instead of the reverse proxy's. Must run before
// UseRateLimiter, which partitions on RemoteIpAddress.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// Metrics endpoint at /metrics (scraped by Prometheus). Placed before auth so Prometheus can
// reach it without a token. The endpoint itself exposes no sensitive business data.
app.UseMetricServer();

// Tracks HTTP request count, duration, and in-progress count per method/route/status.
// Placed early so it captures every request including 4xx/5xx responses.
app.UseHttpMetrics();

// Assign / forward X-Correlation-ID before anything else so every log line carries it.
app.UseMiddleware<CorrelationIdMiddleware>();

// One structured log per request: method, path, status, elapsed ms. TenantId and UserId
// are enriched here via the diagnostic context (resolved by the time the request completes).
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        var tenant = httpContext.RequestServices.GetService<ICurrentTenant>();
        var user = httpContext.RequestServices.GetService<ICurrentUser>();
        diagnosticContext.Set("TenantId", tenant?.TenantId?.ToString() ?? string.Empty);
        diagnosticContext.Set("UserId", user?.UserId?.ToString() ?? string.Empty);
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
    };
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.AddPreferredSecuritySchemes("Bearer");
    });
}

// CORS runs after routing but before tenant resolution and authentication so that a preflight (OPTIONS)
// request — which carries no slug and no token — is answered here and never falls through to tenant or
// auth logic that would reject it.
app.UseCors(CorsPolicies.Spa);

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthentication();
// After UseAuthentication so the tenant_id/sub claims the global limiter partitions on are populated,
// and after routing (added automatically at the pipeline start) so the per-IP policy can read the
// endpoint's [EnableRateLimiting] metadata.
app.UseRateLimiter();
app.UseMiddleware<TenantClaimResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

// Liveness: is the process alive? Predicate = false means no checks run — the endpoint
// just confirms the process can accept connections. A container orchestrator that gets 200
// here knows the app started; it should restart if this ever returns 503.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthCheckJson
});

// Readiness: are all external dependencies reachable? Only checks tagged "ready" run.
// A load balancer should route traffic here only when this returns 200; on 503 it removes
// the instance from rotation until the dependency recovers.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckJson
});

// Hangfire dashboard at /hangfire. LocalRequestsOnlyAuthorizationFilter restricts it to localhost: the
// dashboard exposes job data and trigger/delete controls, and the API's auth is bearer-token based (no
// cookies), so a browser cannot carry a JWT here. Real authentication for a remote dashboard is a
// production hardening task (Sprint 8); in dev, local-only is the correct guard.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new LocalRequestsOnlyAuthorizationFilter()]
});

// Register/refresh the recurring expired-invitation cleanup job. AddOrUpdate keys on the job id, so a
// restart updates the existing schedule rather than creating duplicates.
var hangfireOptions = app.Configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>()
    ?? new HangfireOptions();
RecurringJob.AddOrUpdate<ExpiredInvitationCleanupJob>(
    "expired-invitation-cleanup",
    job => job.CleanupAsync(CancellationToken.None),
    hangfireOptions.ExpiredInvitationCleanupCron,
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.Run();

static Task WriteHealthCheckJson(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            durationMs = e.Value.Duration.TotalMilliseconds,
            description = e.Value.Description
        })
    });
}
