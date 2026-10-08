namespace Ats.Shared.Kernel;

// Every key the email resource files are expected to hold. Constants rather than literals at the
// call sites so a typo is a build error instead of an email that silently ships the key name — and
// so EmailTextResourceTests can walk this class by reflection and assert that both languages define
// all of them. That test is the reason this file is worth its length.
//
// Naming mirrors the JSON: "<area>.<email>.<part>". Each email owns a subject and a body; the body
// is a whole HTML fragment rather than a set of sentence fragments, because a paragraph reads and
// translates as a unit and Turkish frequently needs to reorder across sentence boundaries.
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

        // Used in place of the job title when it is unavailable, so the sentence still reads
        // naturally without announcing that something is missing.
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

        // The two reminders a still-upcoming interview produces. Separate wording rather than one
        // reused template: a day out the useful message is "prepare, or tell us if you cannot make
        // it", and at the door it is "join now" — the same sentence cannot serve both.
        public const string ReminderDayBeforeSubject = "interview.reminder.dayBefore.subject";

        // {0} first name, {1} interview type, {2} job title, {3} when, {4} duration, {5} join line.
        public const string ReminderDayBeforeBody = "interview.reminder.dayBefore.body";

        public const string ReminderStartingSoonSubject = "interview.reminder.startingSoon.subject";

        // Same arguments as the day-before body, so the two stay interchangeable at the call site.
        public const string ReminderStartingSoonBody = "interview.reminder.startingSoon.body";

        // A .NET custom date/time pattern, not a sentence: it belongs with the wording because the
        // field order and the 12/24-hour clock are language decisions, not code decisions.
        public const string DateFormat = "interview.dateFormat";

        // {0} room URL, used twice (href and link text).
        public const string JoinLine = "interview.joinLine";
        public const string JoinLineUnchanged = "interview.joinLineUnchanged";

        // The room is open at the moment this one is sent — the starting-soon reminder is scheduled
        // for exactly Interview.RoomOpensAtUtc — so it invites the candidate in rather than telling
        // them to wait.
        public const string JoinLineNow = "interview.joinLineNow";

        public const string PhoneLineScheduled = "interview.phoneLine.scheduled";
        public const string PhoneLineRescheduled = "interview.phoneLine.rescheduled";
        public const string PhoneLineStartingSoon = "interview.phoneLine.startingSoon";

        // Completed with an InterviewType name, e.g. "interview.type.PhoneScreen". Composed rather
        // than constant because the enum is the source of truth for which values exist.
        public const string TypePrefix = "interview.type.";

        // Completed with an InterviewCancellationReason name. UnknownCancelReason covers a value
        // this build does not recognise — possible when an older consumer meets a newer producer.
        public const string CancelReasonPrefix = "interview.cancelReason.";
        public const string UnknownCancelReason = "interview.cancelReason.unknown";

        // The values that complete the two prefixes above. Listed here, rather than reached for via
        // the Interviews module's enums, because the kernel must not depend on a module — and the
        // resource-parity test needs the full key set from one place.
        public static readonly IReadOnlyList<string> Types =
            ["PhoneScreen", "Technical", "Cultural", "Final"];

        public static readonly IReadOnlyList<string> CancelReasons =
            ["Rescheduling", "CandidateRequested", "CandidateWithdrew", "PositionClosed", "ApplicationRejected"];
    }
}
