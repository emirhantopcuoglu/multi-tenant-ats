using Ats.Modules.Interviews.Domain;
using Ats.Shared.Kernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

public sealed record SubmitInterviewFeedbackCommand(
    Guid InterviewId,
    Guid InterviewerUserId,
    int Rating,
    FeedbackRecommendation Recommendation,
    string? Comments) : ICommand<Guid>;

public sealed class SubmitInterviewFeedbackValidator : AbstractValidator<SubmitInterviewFeedbackCommand>
{
    private const int MinRating = 1;
    private const int MaxRating = 5;

    public SubmitInterviewFeedbackValidator()
    {
        RuleFor(x => x.InterviewId).NotEmpty();
        RuleFor(x => x.InterviewerUserId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(MinRating, MaxRating);
        RuleFor(x => x.Recommendation).IsInEnum();
    }
}

public sealed class SubmitInterviewFeedbackHandler : ICommandHandler<SubmitInterviewFeedbackCommand, Guid>
{
    private readonly IInterviewsDbContext _db;

    public SubmitInterviewFeedbackHandler(IInterviewsDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(SubmitInterviewFeedbackCommand command, CancellationToken ct)
    {
        var interview = await _db.Interviews
            .FirstOrDefaultAsync(i => i.Id == command.InterviewId, ct);

        if (interview is null)
            return Result.Failure<Guid>(InterviewErrors.NotFound);

        // Cancelled interviews never happened; there is nothing to evaluate.
        if (interview.Status == InterviewStatus.Cancelled)
            return Result.Failure<Guid>(InterviewErrors.FeedbackNotEligible);

        var alreadySubmitted = await _db.Feedback.AnyAsync(
            f => f.InterviewId == command.InterviewId
              && f.InterviewerUserId == command.InterviewerUserId, ct);

        if (alreadySubmitted)
            return Result.Failure<Guid>(InterviewErrors.DuplicateFeedback);

        var feedback = InterviewFeedback.Submit(
            command.InterviewId, command.InterviewerUserId,
            command.Rating, command.Recommendation, command.Comments);

        _db.Feedback.Add(feedback);
        await _db.SaveChangesAsync(ct);

        return Result.Success(feedback.Id);
    }
}
