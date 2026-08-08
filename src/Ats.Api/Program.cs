using System.Diagnostics;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Ats.Api;
using Ats.Api.Extensions;
using Prometheus;
using Serilog;
using Serilog.Enrichers.OpenTelemetry;
using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.Tenants.Application;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Minio;
using MongoDB.Driver;
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

builder.AddPersistence();

// LLM-backed CV parsing (Sprint 6.3). The PDF text extractor and the parser are stateless and
// thread-safe (the parser holds one reusable Polly pipeline and pulls HTTP clients from the factory),
// so both are singletons. The parser targets any OpenAI-compatible API; it defaults to GitHub Models
// (free with a GitHub token). The key is read from User Secrets / env via LlmOptions, never from
// appsettings.json.
builder.Services.Configure<LlmOptions>(
    builder.Configuration.GetSection(LlmOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
builder.Services.AddSingleton<IDocxTextExtractor, DocxTextExtractor>();
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

builder.AddRateLimiting();
builder.AddCorsForSpa();

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

builder.AddMessaging();
builder.AddBackgroundJobs();

builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<InvitationOptions>(
    builder.Configuration.GetSection(InvitationOptions.SectionName));
builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
// Singleton: it parses its two embedded JSON files once in the constructor and is immutable
// afterwards, so a per-request instance would re-parse them on every email for no benefit.
builder.Services.AddSingleton<IEmailTextProvider, JsonEmailTextProvider>();
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

builder.AddIdentityAndAuthorization();

builder.AddObservability(redisConnection);

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

app.UseHangfireDashboardEndpoint();
app.MapRecurringJobs();

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
