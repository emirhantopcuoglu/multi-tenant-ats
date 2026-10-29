using Ats.Shared.Infrastructure;
using MongoDB.Driver;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Ats.Api.Extensions;

public static class ObservabilityExtensions
{
    // Tracing and health checks. redisConnection is passed in because AddRedisInstrumentation runs
    // once at configuration time, with nothing to resolve it from.
    public static IHostApplicationBuilder AddObservability(
        this IHostApplicationBuilder builder, IConnectionMultiplexer redisConnection)
    {
        // OTLP export (Jaeger in dev). Vendor-neutral, so changing the backend is only a config change.
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: builder.Configuration["OpenTelemetry:ServiceName"] ?? "ats-api",
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                // Adds spans for MassTransit publish and consume.
                .AddSource("MassTransit")
                .AddRedisInstrumentation(redisConnection)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(
                        builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317");
                }));

        // /health/live has no dependencies; /health/ready checks all of them. The RabbitMQ connection is
        // lazy so an unreachable broker doesn't block startup.
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
