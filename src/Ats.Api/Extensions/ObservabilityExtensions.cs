using Ats.Shared.Infrastructure;
using MongoDB.Driver;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Ats.Api.Extensions;

public static class ObservabilityExtensions
{
    // Distributed tracing (Sprint 7.2) and health checks for liveness/readiness probes. Both answer
    // "what is this service doing / is it doing okay right now" — kept together as one operational
    // visibility concern, unlike e.g. rate limiting and CORS which are unrelated policies.
    //
    // redisConnection is a parameter rather than resolved from DI inside a lambda (the way the rate
    // limiter's per-request closure does it): AddRedisInstrumentation runs once, at TracerProvider
    // configuration time, not per-request, so there is no HttpContext.RequestServices to resolve
    // from. Threading the already-open connection through explicitly is simpler and just as correct.
    public static IHostApplicationBuilder AddObservability(
        this IHostApplicationBuilder builder, IConnectionMultiplexer redisConnection)
    {
        // A single TracerProvider covers all instrumented activities. The OTLP exporter forwards spans
        // to Jaeger (http://localhost:4317 dev); swap the endpoint for Tempo, Honeycomb, or Datadog in
        // other environments — code stays unchanged.
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

        return builder;
    }
}
