using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

public sealed record InterviewFeedbackDto(
    Guid Id, Guid InterviewerUserId, int Rating, string Recommendation,
    string? Comments, DateTime SubmittedAtUtc);

// What the company side sees for one interview's evaluations.
//
// SubmittedCount/ExpectedCount answer "is everyone in yet?" without the caller counting rows it may
// not be allowed to see — which matters precisely because Items can come back empty on purpose.
public sealed record InterviewFeedbackSummaryDto(
    IReadOnlyList<InterviewFeedbackDto> Items,
    int SubmittedCount,
    int ExpectedCount,
    double? AverageRating,
    bool IsWithheld,
    bool HasCallerSubmitted);

public sealed record GetInterviewFeedbackQuery(Guid InterviewId, Guid CallerUserId)
    : IQuery<InterviewFeedbackSummaryDto>;

public sealed class GetInterviewFeedbackHandler
    : IQueryHandler<GetInterviewFeedbackQuery, InterviewFeedbackSummaryDto>
{
    private readonly IInterviewsDbContext _db;

    public GetInterviewFeedbackHandler(IInterviewsDbContext db) => _db = db;

    public async Task<Result<InterviewFeedbackSummaryDto>> Handle(
        GetInterviewFeedbackQuery query, CancellationToken ct)
    {
        var interview = await _db.Interviews.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == query.InterviewId, ct);

        if (interview is null)
            return Result.Failure<InterviewFeedbackSummaryDto>(InterviewErrors.NotFound);

        var feedback = await _db.Feedback.AsNoTracking()
            .Where(f => f.InterviewId == query.InterviewId)
            .OrderBy(f => f.SubmittedAtUtc)
            .Select(f => new InterviewFeedbackDto(
                f.Id, f.InterviewerUserId, f.Rating, f.Recommendation.ToString(),
                f.Comments, f.SubmittedAtUtc))
            .ToListAsync(ct);

        var hasCallerSubmitted = feedback.Any(f => f.InterviewerUserId == query.CallerUserId);

        // An interviewer who has not filed their own evaluation yet sees nobody else's. The entity
        // is deliberately immutable so a score cannot be softened after the fact
        // (InterviewFeedback.cs) — letting an evaluator read the panel first would defeat that by
        // moving the anchoring earlier instead of preventing it. Recruiters and hiring managers who
        // are not on the panel are decision-makers rather than evaluators, so they have no score to
        // be influenced and see everything.
        var isWithheld = interview.InterviewerUserIds.Contains(query.CallerUserId) && !hasCallerSubmitted;

        // The counts are computed before withholding: "1 of 3 submitted" is not a leak, and knowing
        // whether the panel is still pending is exactly what tells a recruiter to chase people.
        var summary = new InterviewFeedbackSummaryDto(
            Items: isWithheld ? [] : feedback,
            SubmittedCount: feedback.Count,
            ExpectedCount: interview.InterviewerUserIds.Count,
            AverageRating: feedback.Count == 0 || isWithheld
                ? null
                : Math.Round(feedback.Average(f => f.Rating), 2),
            IsWithheld: isWithheld,
            HasCallerSubmitted: hasCallerSubmitted);

        return Result.Success(summary);
    }
}
