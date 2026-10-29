namespace Ats.Modules.Interviews.Domain;

public enum InterviewType { PhoneScreen, Technical, Cultural, Final }

// Scheduled is the only non-terminal state.
public enum InterviewStatus { Scheduled, Completed, Cancelled, NoShow }

public enum InterviewReminderKind { DayBefore, StartingSoon }

// Ordered from most negative to most positive.
public enum FeedbackRecommendation { StrongNoHire, NoHire, Hire, StrongHire }

// Separate from the status: a candidate no-show and an interviewer no-show mean different things.
public enum NoShowParty { Candidate, Interviewer }

// Candidate-facing, so a closed set. The recruiter's own text is Interview.CancellationNote.
public enum InterviewCancellationReason
{
    // The only reason that promises a new invitation.
    Rescheduling,

    CandidateRequested,
    CandidateWithdrew,
    PositionClosed,

    // Set by the system when the application is rejected; not offered in the cancel dialog.
    ApplicationRejected,

    Other,
}
