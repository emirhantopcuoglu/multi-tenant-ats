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
    // RabbitMQ message bus (Sprint 5). MassTransit is the abstraction over the broker: it owns the
    // connection, retries, and (Sprint 5.3) the outbox, and lets consumer code stay transport-agnostic.
    // Sprint 5.2 added the first consumer (application-submitted -> candidate email). Unlike the
    // Mongo/MinIO initializers, MassTransit's hosted service connects in the background and retries on
    // its own, so a broker that is briefly unreachable does not crash startup.
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
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
            // CONSTRAINT: exactly one bus outbox per container in MassTransit 8.x. UseBusOutbox routes
            // every scoped IPublishEndpoint into this DbContext; registering a second one (e.g. for
            // InterviewsDbContext) silently replaces this registration, and every Applications publish
            // then lands in a context that request never saves — messages vanish without an error.
            // Verified empirically before the Interviews module's outbox was rolled back. Modules other
            // than Applications must publish via IBus (direct, after their own SaveChanges) until the
            // stack supports multiple bus outboxes (MassTransit v9.1+, commercial).
            bus.AddEntityFrameworkOutbox<ApplicationsDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            // Notifications consumers: email the candidate when an application is submitted, rejected,
            // hired, moved to a new stage, or gets an interview scheduled (roadmap 3.4). ConfigureEndpoints
            // below creates and binds each consumer's queue automatically.
            bus.AddConsumer<ApplicationSubmittedConsumer>();
            bus.AddConsumer<ApplicationRejectedConsumer>();
            bus.AddConsumer<ApplicationHiredConsumer>();
            bus.AddConsumer<ApplicationStageChangedEmailConsumer>();
            bus.AddConsumer<InterviewScheduledEmailConsumer>();
            // An interview the candidate already has in their calendar must never move or vanish silently.
            bus.AddConsumer<InterviewRescheduledEmailConsumer>();
            bus.AddConsumer<InterviewCancelledEmailConsumer>();
            // Driven by the reminder sweep rather than by a recruiter action; see InterviewReminderJob.
            bus.AddConsumer<InterviewReminderEmailConsumer>();

            // In-app notification writers (FAZ 3): each event lands in its own queue, independent of the
            // email consumers above, and becomes a row behind the candidate's bell icon.
            bus.AddConsumer<ApplicationStageChangedNotificationConsumer>();
            bus.AddConsumer<InterviewScheduledNotificationConsumer>();
            bus.AddConsumer<InterviewRescheduledNotificationConsumer>();
            bus.AddConsumer<InterviewCancelledNotificationConsumer>();
            bus.AddConsumer<InterviewReminderNotificationConsumer>();
            bus.AddConsumer<ApplicationViewedNotificationConsumer>();
            bus.AddConsumer<ApplicationCvDownloadedNotificationConsumer>();
            bus.AddConsumer<NewApplicationNotificationConsumer>();

            // CV-parsing consumer (Sprint 6.3): downloads the CV, extracts text, asks an LLM for structured
            // data, and stores it in MongoDB. The provider is OpenAI-compatible and selected entirely
            // through the Llm configuration section — naming a vendor here is what made this comment wrong
            // for months after the provider changed. Inherits the retry/dead-letter policy configured below.
            bus.AddConsumer<CvParsingConsumer>();

            // Pipeline/interview consistency: advances an application into its pipeline's Interview stage
            // when a recruiter schedules an interview against it, and calls off upcoming interviews when the
            // application behind them closes — whether the company rejected it or the candidate withdrew.
            // Own queues, independent of the notifications above; the closed-application consumer handles
            // two message types and so gets one queue per type.
            // The stage-advance consumer carries a definition: it publishes a follow-up event, so it runs
            // behind the transactional outbox to keep the move and the announcement atomic. See
            // AdvanceToInterviewStageConsumerDefinition.
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

                // Consumer retry policy (Sprint 5.5). Applied here, before ConfigureEndpoints, so every consumer
                // endpoint inherits it: a throwing consumer is retried with an exponential back-off instead of
                // failing once or looping forever. intervalDelta is set to the initial interval so the back-off
                // grows from there. When all attempts are exhausted, MassTransit moves the message to the
                // endpoint's "<queue>_error" dead-letter queue automatically — no extra wiring needed.
                configurator.UseMessageRetry(retry =>
                {
                    retry.Exponential(
                        retryLimit: rabbitMqOptions.RetryLimit,
                        minInterval: TimeSpan.FromSeconds(rabbitMqOptions.RetryInitialIntervalSeconds),
                        maxInterval: TimeSpan.FromSeconds(rabbitMqOptions.RetryMaxIntervalSeconds),
                        intervalDelta: TimeSpan.FromSeconds(rabbitMqOptions.RetryInitialIntervalSeconds));

                    // A rejected LLM key is a configuration fault, not a transient one: no number of
                    // redeliveries fixes it, and the back-off only delays the dead-letter that makes it
                    // visible. Straight to the error queue, where the message waits to be replayed once the
                    // key is reissued.
                    retry.Ignore<LlmAuthenticationException>();
                });

                // Auto-wires every registered consumer to its endpoint and binds the retry policy above.
                configurator.ConfigureEndpoints(context);
            });
        });

        // Message idempotency guard (Sprint 5.5). RabbitMQ delivers at-least-once, so a consumer can see the
        // same message twice (a lost ack, a retry, or an error-queue replay). The guard marks a processed
        // message in Redis so a duplicate delivery does not send a duplicate email. It reuses the shared
        // Redis multiplexer registered as a singleton at startup; it is stateless, so a singleton is fine.
        builder.Services.Configure<IdempotencyOptions>(
            builder.Configuration.GetSection(IdempotencyOptions.SectionName));
        builder.Services.AddSingleton<IIdempotencyGuard, RedisIdempotencyGuard>();

        return builder;
    }
}
