using System.Net;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Emails the candidate when a scheduled interview is called off.
//
// The reason decides one thing that actually matters to the reader: whether another invitation is
// coming. "Your interview is cancelled" with no answer to that leaves someone refreshing their
// inbox for a week, which is why the reason travels on the event at all. The recruiter's internal
// note is not on the contract, so it cannot be rendered here even by mistake.
public sealed class InterviewCancelledEmailConsumer
    : IConsumer<InterviewCancelledIntegrationEvent>
{
    private const string Subject = "Your interview has been cancelled";

    private readonly IEmailSender _emailSender;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<InterviewCancelledEmailConsumer> _logger;

    public InterviewCancelledEmailConsumer(
        IEmailSender emailSender,
        IIdempotencyGuard idempotencyGuard,
        ILogger<InterviewCancelledEmailConsumer> logger)
    {
        _emailSender = emailSender;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewCancelledIntegrationEvent> context)
    {
        var message = context.Message;

        var firstName = WebUtility.HtmlEncode(message.CandidateFirstName);
        var jobTitle = WebUtility.HtmlEncode(message.JobTitle);
        var interviewType = InterviewEmailFormatting.HumanizeType(message.InterviewType);
        var scheduledAt = InterviewEmailFormatting.FormatUtc(message.ScheduledAtUtc);

        var body = $"""
            <p>Hi {firstName},</p>
            <p>Your <strong>{interviewType}</strong> interview for <strong>{jobTitle}</strong>,
            scheduled for {scheduledAt}, has been cancelled.</p>
            <p>{ClosingFor(message.Reason)}</p>
            """;

        var key = $"notifications:interview-cancelled-email:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key,
            () => _emailSender.SendAsync(message.CandidateEmail, Subject, body, context.CancellationToken));

        if (!sent)
        {
            _logger.LogInformation(
                "Skipped duplicate interview-cancelled email for interview {InterviewId}",
                message.InterviewId);
            return;
        }

        _logger.LogInformation(
            "Sent interview-cancelled email to {CandidateEmail} for interview {InterviewId}",
            message.CandidateEmail,
            message.InterviewId);
    }

    // Reason arrives as a string across the contract boundary, so an unrecognised value is possible
    // (an older consumer against a newer producer). It falls back to the neutral sentence rather
    // than throwing: a slightly vague email beats a poisoned message and no email at all.
    //
    // Public rather than internal so the wording can be tested without a ConsumeContext and without
    // an InternalsVisibleTo just for tests — the same call AdvanceToInterviewStageConsumer makes.
    public static string ClosingFor(string reason) => reason switch
    {
        "Rescheduling" =>
            "We will be in touch shortly with a new time — there is nothing you need to do.",
        "CandidateRequested" =>
            "This was cancelled at your request. Let us know when you would like to rearrange.",
        "CandidateWithdrew" =>
            "This follows your decision to withdraw from the process. Thank you for your time, "
            + "and we hope to hear from you about future roles.",
        "PositionClosed" =>
            "The position is no longer open, so this interview will not be rearranged. "
            + "Thank you for the time you invested, and we are sorry for the disappointing news.",
        "ApplicationRejected" =>
            "Your application is no longer moving forward, so this interview will not go ahead. "
            + "Thank you for the time you invested in the process.",
        _ =>
            "If another time becomes available we will be in touch. "
            + "Thank you for your patience.",
    };
}
