using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

public sealed record InterviewFeedbackDto(
    Guid Id, Guid InterviewerUserId, int Rating, string Recommendation,
    string? Comments, DateTime SubmittedAtUtc);

// Counts are always returned, even when Items is withheld.
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

        // A panel member sees nobody else's feedback until they submit their own, so they can't be
        // anchored by it. People not on the panel see everything.
        var isWithheld = interview.InterviewerUserIds.Contains(query.CallerUserId) && !hasCallerSubmitted;

        // Counts are computed before withholding; "1 of 3 submitted" isn't a leak.
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
