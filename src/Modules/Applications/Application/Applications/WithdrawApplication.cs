using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Kernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Applications.Application.Applications;

// The candidate closes their own application. The account id comes from the candidate token.
// No reason field: unlike a rejection, nobody needs to read one later.
public sealed record WithdrawApplicationCommand(Guid CandidateAccountId, Guid ApplicationId)
    : ICommand<bool>;

public sealed class WithdrawApplicationValidator : AbstractValidator<WithdrawApplicationCommand>
{
    public WithdrawApplicationValidator()
    {
        RuleFor(x => x.CandidateAccountId).NotEmpty();
        RuleFor(x => x.ApplicationId).NotEmpty();
    }
}

public sealed class WithdrawApplicationHandler : ICommandHandler<WithdrawApplicationCommand, bool>
{
    private readonly IApplicationsDbContext _db;
    private readonly IPublisher _publisher;
    private readonly IActivityLogRepository _activityLog;
    private readonly ILogger<WithdrawApplicationHandler> _logger;

    public WithdrawApplicationHandler(
        IApplicationsDbContext db,
        IPublisher publisher,
        IActivityLogRepository activityLog,
        ILogger<WithdrawApplicationHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(WithdrawApplicationCommand command, CancellationToken ct)
    {
        // Ownership is part of the WHERE, so someone else's id looks like a missing one. The tenant
        // filter is bypassed: a candidate request has no tenant. Tracked because the row is updated.
        var application = await _db.Applications
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                a => !a.IsDeleted
                     && a.Id == command.ApplicationId
                     && a.CandidateAccountId == command.CandidateAccountId,
                ct);

        if (application is null)
            return Result.Failure<bool>(ApplicationErrors.NotFound);

        // Checked separately so "already closed" (409) stays distinct from "not yours" (404).
        if (application.Status != ApplicationStatus.Active)
            return Result.Failure<bool>(ApplicationErrors.NotWithdrawable);

        // No stage move: the board only shows Active applications, so it drops off on its own.
        application.Withdraw();

        // Published before SaveChanges so the outbox message commits with the status change.
        await _publisher.Publish(
            new ApplicationWithdrawnEvent(application.Id, application.TenantId), ct);

        await _db.SaveChangesAsync(ct);

        // After the save. Use the explicit-tenant overload: the ambient one throws on a candidate request,
        // and TryAddAsync would hide that.
        await _activityLog.TryAddAsync(
            ApplicationActivity.Withdrawn(application.Id), application.TenantId, _logger, ct);

        return Result.Success(true);
    }
}
