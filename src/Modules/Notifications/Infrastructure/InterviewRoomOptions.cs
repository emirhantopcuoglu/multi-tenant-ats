namespace Ats.Modules.Notifications.Infrastructure;

public sealed class InterviewRoomOptions
{
    public const string SectionName = "InterviewRoom";

    public string BaseUrl { get; init; } = "http://localhost:5173/interview-room";
}
