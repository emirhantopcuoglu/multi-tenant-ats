using Ats.Shared.Kernel;

namespace Ats.Modules.Interviews.Domain;

// Feedback submitted by one interviewer after an interview. Not soft-deleted: feedback is a
// permanent record and removing it would silently erase hiring signal.
// Each (InterviewId, InterviewerUserId) pair is unique — enforced by a DB index. The domain
// guard here is intentional belt-and-suspenders for tests and in-memory scenarios.
public sealed class InterviewFeedback : ITenantScoped, IAuditable
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InterviewId { get; private set; }
    public Guid InterviewerUserId { get; private set; }
    public int Rating { get; private set; }
    public FeedbackRecommendation Recommendation { get; private set; }
    public string? Comments { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }

    // IAuditable — written by the interceptor, not application code.
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    private InterviewFeedback() { }

    public static InterviewFeedback Submit(
        Guid interviewId,
        Guid interviewerUserId,
        int rating,
        FeedbackRecommendation recommendation,
        string? comments)
    {
        if (interviewId == Guid.Empty)
            throw new ArgumentException("Interview ID is required.", nameof(interviewId));
        if (interviewerUserId == Guid.Empty)
            throw new ArgumentException("Interviewer user ID is required.", nameof(interviewerUserId));
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");

        return new InterviewFeedback
        {
            Id = Guid.NewGuid(),
            InterviewId = interviewId,
            InterviewerUserId = interviewerUserId,
            Rating = rating,
            Recommendation = recommendation,
            Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(),
            SubmittedAtUtc = DateTime.UtcNow
        };
    }
}
