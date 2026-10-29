using Ats.Modules.Interviews.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

// Summary of an application's interviews. AwaitingOutcomeCount is interviews whose slot passed
// with nothing recorded.
public sealed record ApplicationInterviewOutcomeDto(
    int TotalCount,
    int CompletedCount,
    int AwaitingOutcomeCount,
    int FeedbackCount,
    int ExpectedFeedbackCount,
    double? AverageRating,
    // Recommendation name to count, so a new recommendation doesn't change the shape.
    IReadOnlyDictionary<string, int> RecommendationCounts);

public sealed record GetApplicationInterviewOutcomeQuery(Guid ApplicationId)
    : IQuery<ApplicationInterviewOutcomeDto>;

public sealed class GetApplicationInterviewOutcomeHandler
    : IQueryHandler<GetApplicationInterviewOutcomeQuery, ApplicationInterviewOutcomeDto>
{
    private static readonly ApplicationInterviewOutcomeDto Empty =
        new(0, 0, 0, 0, 0, null, new Dictionary<string, int>());

    private readonly IInterviewsDbContext _db;

    public GetApplicationInterviewOutcomeHandler(IInterviewsDbContext db) => _db = db;

    public async Task<Result<ApplicationInterviewOutcomeDto>> Handle(
        GetApplicationInterviewOutcomeQuery query, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;

        var interviews = await _db.Interviews.AsNoTracking()
            .Where(i => i.ApplicationId == query.ApplicationId)
            .ToListAsync(ct);

        if (interviews.Count == 0)
            return Result.Success(Empty);

        var interviewIds = interviews.Select(i => i.Id).ToList();
        var feedback = await _db.Feedback.AsNoTracking()
            .Where(f => interviewIds.Contains(f.InterviewId))
            .ToListAsync(ct);

        // Cancelled and no-show interviews have nothing to evaluate, so they don't count as missing feedback.
        var expectedFeedback = interviews
            .Where(i => i.Status == InterviewStatus.Completed || i.IsAwaitingOutcome(nowUtc))
            .Sum(i => i.InterviewerUserIds.Count);

        return Result.Success(new ApplicationInterviewOutcomeDto(
            TotalCount: interviews.Count,
            CompletedCount: interviews.Count(i => i.Status == InterviewStatus.Completed),
            AwaitingOutcomeCount: interviews.Count(i => i.IsAwaitingOutcome(nowUtc)),
            FeedbackCount: feedback.Count,
            ExpectedFeedbackCount: expectedFeedback,
            AverageRating: feedback.Count == 0 ? null : Math.Round(feedback.Average(f => f.Rating), 2),
            RecommendationCounts: feedback
                .GroupBy(f => f.Recommendation.ToString())
                .ToDictionary(g => g.Key, g => g.Count())));
    }
}
