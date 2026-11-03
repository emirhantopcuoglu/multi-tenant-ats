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

        // Only interviews that have happened can be evaluated.
        if (!interview.CanReceiveFeedback(DateTime.UtcNow))
            return Result.Failure<Guid>(InterviewErrors.FeedbackNotEligible);

        if (await IsAlreadySubmittedAsync(command, ct))
            return Result.Failure<Guid>(InterviewErrors.DuplicateFeedback);

        var feedback = InterviewFeedback.Submit(
            command.InterviewId, command.InterviewerUserId,
            command.Rating, command.Recommendation, command.Comments);

        _db.Feedback.Add(feedback);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A parallel submission saved first and the unique index refused this one.
            if (!await IsAlreadySubmittedAsync(command, ct))
                throw;
            return Result.Failure<Guid>(InterviewErrors.DuplicateFeedback);
        }

        return Result.Success(feedback.Id);
    }

    private Task<bool> IsAlreadySubmittedAsync(SubmitInterviewFeedbackCommand command, CancellationToken ct) =>
        _db.Feedback.AnyAsync(
            f => f.InterviewId == command.InterviewId
              && f.InterviewerUserId == command.InterviewerUserId, ct);
}
