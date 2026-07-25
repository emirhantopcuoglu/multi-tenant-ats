namespace Ats.Modules.Interviews.Domain;

// The kind of interview, used to label it and (later) to drive type-specific reminders or templates.
public enum InterviewType { PhoneScreen, Technical, Cultural, Final }

// Lifecycle of a single interview. Scheduled is the only state from which the others are reachable;
// Completed, Cancelled and NoShow are terminal and cannot transition further.
public enum InterviewStatus { Scheduled, Completed, Cancelled, NoShow }

// Structured hiring recommendation attached to a piece of feedback. Ordered from most negative
// to most positive so comparisons and displays can sort naturally.
public enum FeedbackRecommendation { StrongNoHire, NoHire, Hire, StrongHire }

// Why a scheduled interview was called off. A closed set rather than free text because this value
// is candidate-facing: it selects the sentence the cancellation email leads with, and the one thing
// a candidate actually needs to know is whether another invitation is coming. The recruiter's own
// wording lives in Interview.CancellationNote and never leaves the company side.
public enum InterviewCancellationReason
{
    // A replacement will be booked. The only reason that promises a follow-up.
    Rescheduling,

    CandidateRequested,
    CandidateWithdrew,
    PositionClosed,
    Other,
}
