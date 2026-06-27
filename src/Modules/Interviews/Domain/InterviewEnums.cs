namespace Ats.Modules.Interviews.Domain;

// The kind of interview, used to label it and (later) to drive type-specific reminders or templates.
public enum InterviewType { PhoneScreen, Technical, Cultural, Final }

// Lifecycle of a single interview. Scheduled is the only state from which the others are reachable;
// Completed, Cancelled and NoShow are terminal and cannot transition further.
public enum InterviewStatus { Scheduled, Completed, Cancelled, NoShow }

// Structured hiring recommendation attached to a piece of feedback. Ordered from most negative
// to most positive so comparisons and displays can sort naturally.
public enum FeedbackRecommendation { StrongNoHire, NoHire, Hire, StrongHire }
