namespace Ats.Shared.Kernel;

// Constants so a typo is a build error. EmailTextResourceTests checks both languages define all
// of them. Each body is a whole HTML fragment so it translates as one unit.
public static class EmailTextKeys
{
    public static class Candidate
    {
        public const string VerifyEmailSubject = "candidate.verifyEmail.subject";

        // {0} first name, {1} confirmation link, {2} hours the link stays valid.
        public const string VerifyEmailBody = "candidate.verifyEmail.body";

        public const string ResetPasswordSubject = "candidate.resetPassword.subject";

        // {0} reset link.
        public const string ResetPasswordBody = "candidate.resetPassword.body";

        public const string PasswordResetSubject = "candidate.passwordReset.subject";
        public const string PasswordResetBody = "candidate.passwordReset.body";

        public const string PasswordChangedSubject = "candidate.passwordChanged.subject";
        public const string PasswordChangedBody = "candidate.passwordChanged.body";

        public const string EmailChangeConfirmSubject = "candidate.emailChangeConfirm.subject";

        // {0} confirmation link.
        public const string EmailChangeConfirmBody = "candidate.emailChangeConfirm.body";

        public const string EmailChangedSubject = "candidate.emailChanged.subject";
        public const string EmailChangedBody = "candidate.emailChanged.body";
    }

    public static class Company
    {
        public const string ConfirmEmailSubject = "company.confirmEmail.subject";

        // {0} first name, {1} confirmation link, {2} hours the link stays valid.
        public const string ConfirmEmailBody = "company.confirmEmail.body";

        public const string ResetPasswordSubject = "company.resetPassword.subject";

        // {0} reset link, {1} minutes the link stays valid.
        public const string ResetPasswordBody = "company.resetPassword.body";

        public const string PasswordResetSubject = "company.passwordReset.subject";
        public const string PasswordResetBody = "company.passwordReset.body";

        public const string InvitationSubject = "company.invitation.subject";

        // {0} role name, {1} accept link, {2} days the invitation stays valid.
        public const string InvitationBody = "company.invitation.body";
    }

    public static class Application
    {
        public const string SubmittedSubject = "application.submitted.subject";

        // {0} first name, {1} job title.
        public const string SubmittedBody = "application.submitted.body";

        public const string RejectedSubject = "application.rejected.subject";

        // {0} first name, {1} role phrase (job title or the fallback below).
        public const string RejectedBody = "application.rejected.body";

        public const string HiredSubject = "application.hired.subject";

        // {0} first name, {1} role phrase.
        public const string HiredBody = "application.hired.body";

        public const string StageChangedSubject = "application.stageChanged.subject";

        // {0} first name, {1} job title, {2} new stage name.
        public const string StageChangedBody = "application.stageChanged.body";

        // Used when the job title is missing.
        public const string FallbackRole = "application.fallbackRole";
    }

    public static class Interview
    {
        public const string ScheduledSubject = "interview.scheduled.subject";

        // {0} first name, {1} job title, {2} interview type, {3} when, {4} duration, {5} join line.
        public const string ScheduledBody = "interview.scheduled.body";

        public const string RescheduledSubject = "interview.rescheduled.subject";

        // {0} first name, {1} interview type, {2} job title, {3} previous time, {4} new time,
        // {5} duration, {6} join line.
        public const string RescheduledBody = "interview.rescheduled.body";

        public const string CancelledSubject = "interview.cancelled.subject";

        // {0} first name, {1} interview type, {2} job title, {3} when, {4} closing sentence.
        public const string CancelledBody = "interview.cancelled.body";

        public const string ReminderDayBeforeSubject = "interview.reminder.dayBefore.subject";

        // {0} first name, {1} interview type, {2} job title, {3} when, {4} duration, {5} join line.
        public const string ReminderDayBeforeBody = "interview.reminder.dayBefore.body";

        public const string ReminderStartingSoonSubject = "interview.reminder.startingSoon.subject";

        // Same arguments as the day-before body.
        public const string ReminderStartingSoonBody = "interview.reminder.startingSoon.body";

        // A .NET date pattern; field order and the clock format are language choices.
        public const string DateFormat = "interview.dateFormat";

        // {0} room URL, used twice (href and link text).
        public const string JoinLine = "interview.joinLine";
        public const string JoinLineUnchanged = "interview.joinLineUnchanged";

        // Sent when the room opens, so it says "join now".
        public const string JoinLineNow = "interview.joinLineNow";

        public const string PhoneLineScheduled = "interview.phoneLine.scheduled";
        public const string PhoneLineRescheduled = "interview.phoneLine.rescheduled";
        public const string PhoneLineStartingSoon = "interview.phoneLine.startingSoon";

        // Followed by an InterviewType name, e.g. "interview.type.PhoneScreen".
        public const string TypePrefix = "interview.type.";

        // Followed by an InterviewCancellationReason name. UnknownCancelReason covers unrecognized values.
        public const string CancelReasonPrefix = "interview.cancelReason.";
        public const string UnknownCancelReason = "interview.cancelReason.unknown";

        // Listed here because the kernel can't reference a module's enums.
        public static readonly IReadOnlyList<string> Types =
            ["PhoneScreen", "Technical", "Cultural", "Final"];

        public static readonly IReadOnlyList<string> CancelReasons =
            ["Rescheduling", "CandidateRequested", "CandidateWithdrew", "PositionClosed", "ApplicationRejected"];
    }
}
