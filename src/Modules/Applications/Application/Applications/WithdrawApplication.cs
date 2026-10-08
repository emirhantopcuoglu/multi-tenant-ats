using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Kernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Applications.Application.Applications;

// The candidate closes their own application. The counterpart of RejectApplicationCommand, but owned
// by the other side of the marketplace: the acting identity is a CandidateAccount, not a company
// user, so the id travels in the command rather than coming from ICurrentUser — the controller reads
// it from the candidate token and this handler treats it as the authorization scope.
//
// Deliberately no reason field. A rejection needs one because the recruiter's own colleagues read the
// pipeline later; a withdrawal is the candidate's decision about their own time, and asking them to
// justify it would be the product asking for something it has no use for.
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
        // Ownership is part of the WHERE, exactly as in GetCandidateApplicationDetailQuery: an
        // application that exists but belongs to someone else is indistinguishable from one that does
        // not exist, so ids reveal nothing when probed. The tenant filter is bypassed on purpose —
        // the global candidate account is the scope root here, and a candidate request carries no
        // ambient tenant at all. No AsNoTracking: this row is about to be mutated.
        var application = await _db.Applications
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                a => !a.IsDeleted
                     && a.Id == command.ApplicationId
                     && a.CandidateAccountId == command.CandidateAccountId,
                ct);

        if (application is null)
            return Result.Failure<bool>(ApplicationErrors.NotFound);

        // Status is checked here rather than folded into the query above so "already closed" stays
        // distinguishable from "not yours". Withdrawing twice is a stale tab, not a probe, and the
        // portal should say so instead of claiming the application vanished.
        if (application.Status != ApplicationStatus.Active)
            return Result.Failure<bool>(ApplicationErrors.NotWithdrawable);

        // No stage move, unlike Reject and Hire: there is no FinalWithdrawn stage type, and none is
        // needed. The board query filters to Active applications, so a withdrawn one leaves the
        // columns on its own — the status and the board still agree without parking it anywhere.
        application.Withdraw();

        // Published before SaveChanges so the transactional outbox writes the message in the same
        // transaction as the status change; the Interviews module then releases the booked slots.
        // TenantId comes off the row we just verified, never from the caller.
        await _publisher.Publish(
            new ApplicationWithdrawnEvent(application.Id, application.TenantId), ct);

        await _db.SaveChangesAsync(ct);

        // Best-effort after commit, as everywhere in this module: the activity log lives in MongoDB,
        // outside this transaction, and a failed log write must not undo the withdrawal.
        //
        // The explicit-tenant overload is mandatory here. The ambient one reads ICurrentTenant and
        // throws when nothing is resolved — which is always, on a candidate request — and TryAddAsync
        // would swallow that into a warning, leaving the withdrawal permanently missing from both
        // timelines with a green response.
        await _activityLog.TryAddAsync(
            ApplicationActivity.Withdrawn(application.Id), application.TenantId, _logger, ct);

        return Result.Success(true);
    }
}
