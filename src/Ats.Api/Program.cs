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

// Serilog starts before the host so startup errors are logged too. Destructure.With masks
// sensitive properties.
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, config) => config
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    // Adds TraceId and SpanId so log lines can be matched to traces.
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

// Order matters: the validation handler runs first.
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

// Both are stateless, so singletons. The API key comes from User Secrets or environment variables.
builder.Services.Configure<LlmOptions>(
    builder.Configuration.GetSection(LlmOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
builder.Services.AddSingleton<IDocxTextExtractor, DocxTextExtractor>();
builder.Services.AddSingleton<ICvParser, OpenAiCompatibleCvParser>();

// One Redis connection for the whole app. AbortOnConnectFail = false so Redis being down doesn't
// stop startup.
var redisOptions = builder.Configuration
    .GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
var redisConfiguration = ConfigurationOptions.Parse(redisOptions.ConnectionString);
redisConfiguration.AbortOnConnectFail = false;
// Fail Redis commands immediately while it is down instead of waiting for the timeout; everything
// using Redis is fail-open.
redisConfiguration.BacklogPolicy = BacklogPolicy.FailFast;
var redisConnection = ConnectionMultiplexer.Connect(redisConfiguration);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);

// Caches the slug -> tenant lookup. Best-effort.
builder.Services.AddStackExchangeRedisCache(options =>
    options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(redisConnection));

builder.AddRateLimiting();
builder.AddCorsForSpa();

// Behind the proxy the client IP is in X-Forwarded-For, and the per-IP limiter depends on it.
// Known networks are cleared because the proxy's Docker network is unknown; only safe while the API
// is never exposed directly.
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
builder.Services.AddSingleton<IEmailTextProvider, JsonEmailTextProvider>();
builder.Services.AddScoped<IInvitationService, InvitationService>();

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

    // Startup needs MinIO, like migrations and role seeding above. Fine for dev.
    var minioClient = scope.ServiceProvider.GetRequiredService<IMinioClient>();
    var fileStorageOptions = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>();
    await FileStorageInitializer.EnsureBucketAsync(minioClient, fileStorageOptions);

    var mongoDatabase = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    await MongoActivityLogInitializer.EnsureIndexesAsync(mongoDatabase);
    await MongoAuditLogInitializer.EnsureIndexesAsync(mongoDatabase);
}

// First, so logging, metrics and the rate limiter see the real client IP.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// Before auth so Prometheus doesn't need a token. No business data here.
app.UseMetricServer();

app.UseHttpMetrics();

// Before anything else so every log line has it.
app.UseMiddleware<CorrelationIdMiddleware>();

// One log line per request, with TenantId and UserId added at the end.
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

// Before tenant resolution and auth, so preflight requests (no slug, no token) are answered here.
app.UseCors(CorsPolicies.Spa);

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthentication();
// After authentication so the claims it partitions on are set.
app.UseRateLimiter();
app.UseMiddleware<TenantClaimResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

// No checks: only confirms the process is up.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthCheckJson
});

// Only checks tagged "ready".
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
