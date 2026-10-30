using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Options;

namespace Ats.Api.Extensions;

public static class MessagingExtensions
{
    // MassTransit connects in the background and retries, so an unreachable broker doesn't stop startup.
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<RabbitMqOptions>(
            builder.Configuration.GetSection(RabbitMqOptions.SectionName));
        builder.Services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            // Transactional outbox: messages are written to the applications schema in the same SaveChanges
            // and delivered afterwards. MassTransit 8.x allows only one bus outbox per container; a second
            // UseBusOutbox silently replaces this one and Applications messages are lost. Other modules
            // publish through IBus after their own save.
            bus.AddEntityFrameworkOutbox<ApplicationsDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            // Email consumers. ConfigureEndpoints below creates their queues.
            bus.AddConsumer<ApplicationSubmittedConsumer>();
            bus.AddConsumer<ApplicationRejectedConsumer>();
            bus.AddConsumer<ApplicationHiredConsumer>();
            bus.AddConsumer<ApplicationStageChangedEmailConsumer>();
            bus.AddConsumer<InterviewScheduledEmailConsumer>();
            bus.AddConsumer<InterviewRescheduledEmailConsumer>();
            bus.AddConsumer<InterviewCancelledEmailConsumer>();
            // Driven by the reminder sweep; see InterviewReminderJob.
            bus.AddConsumer<InterviewReminderEmailConsumer>();

            // In-app notification writers, each on its own queue.
            bus.AddConsumer<ApplicationStageChangedNotificationConsumer>();
            bus.AddConsumer<InterviewScheduledNotificationConsumer>();
            bus.AddConsumer<InterviewRescheduledNotificationConsumer>();
            bus.AddConsumer<InterviewCancelledNotificationConsumer>();
            bus.AddConsumer<InterviewReminderNotificationConsumer>();
            bus.AddConsumer<ApplicationViewedNotificationConsumer>();
            bus.AddConsumer<ApplicationCvDownloadedNotificationConsumer>();
            bus.AddConsumer<NewApplicationNotificationConsumer>();

            // Provider is set by the Llm config section.
            bus.AddConsumer<CvParsingConsumer>();

            // Keeps pipelines in step with interviews. The stage-advance consumer publishes a follow-up event,
            // so it runs behind the outbox (see its definition).
            bus.AddConsumer<AdvanceToInterviewStageConsumer, AdvanceToInterviewStageConsumerDefinition>();
            bus.AddConsumer<CancelInterviewsOnApplicationClosedConsumer>();

            bus.UsingRabbitMq((context, configurator) =>
            {
                var rabbitMqOptions = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                configurator.Host(rabbitMqOptions.Host, rabbitMqOptions.Port, rabbitMqOptions.VirtualHost, host =>
                {
                    host.Username(rabbitMqOptions.Username);
                    host.Password(rabbitMqOptions.Password);
                });

                // Before ConfigureEndpoints so every consumer gets it. After the last attempt the message goes to
                // the "<queue>_error" queue.
                configurator.UseMessageRetry(retry =>
                {
                    retry.Exponential(
                        retryLimit: rabbitMqOptions.RetryLimit,
                        minInterval: TimeSpan.FromSeconds(rabbitMqOptions.RetryInitialIntervalSeconds),
                        maxInterval: TimeSpan.FromSeconds(rabbitMqOptions.RetryMaxIntervalSeconds),
                        intervalDelta: TimeSpan.FromSeconds(rabbitMqOptions.RetryInitialIntervalSeconds));

                    // A rejected LLM key won't fix itself; send it straight to the error queue.
                    retry.Ignore<LlmAuthenticationException>();
                });

                configurator.ConfigureEndpoints(context);
            });
        });

        // Delivery is at-least-once; the guard keeps a duplicate from sending a duplicate email.
        builder.Services.Configure<IdempotencyOptions>(
            builder.Configuration.GetSection(IdempotencyOptions.SectionName));
        builder.Services.AddSingleton<IIdempotencyGuard, RedisIdempotencyGuard>();

        return builder;
    }
}
