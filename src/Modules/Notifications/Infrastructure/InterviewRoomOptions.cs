namespace Ats.Modules.Notifications.Infrastructure;

// Where the emailed room link points. This is a frontend URL, so it is configuration, not code —
// same pattern as CandidateEmailChangeOptions.ConfirmBaseUrl. The actual join endpoint the SPA page
// calls at that route lives in the Interviews module; this option only builds the link text.
public sealed class InterviewRoomOptions
{
    public const string SectionName = "InterviewRoom";

    public string BaseUrl { get; init; } = "http://localhost:5173/interview-room";
}
