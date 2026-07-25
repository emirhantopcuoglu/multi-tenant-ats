using Ats.Modules.Interviews.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

// Whether an application's interviews are finished and what they concluded — the signal a stage
// decision should rest on. Feedback existed but only per interview, so deciding meant opening each
// one in turn and holding the scores in your head.
//
// AwaitingOutcomeCount is the number whose slot has passed with nothing recorded. Deciding while
// those are outstanding means deciding on incomplete information, which is worth saying out loud.
public sealed record ApplicationInterviewOutcomeDto(
    int TotalCount,
    int CompletedCount,
    int AwaitingOutcomeCount,
    int FeedbackCount,
    int ExpectedFeedbackCount,
    double? AverageRating,
    // Recommendation name -> how many interviewers gave it. A dictionary rather than one field per
    // value so adding a recommendation does not change this shape.
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

        // Only interviews that happened can be evaluated, so only they count towards the expected
        // total. A cancelled or no-show interview is not missing feedback — there was nothing to
        // evaluate, and counting it would make a complete panel look permanently incomplete.
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
