using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Kernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// "Application" is both the aggregate and this layer's namespace; alias the type so
// Application.Create below resolves to the entity, not the namespace.
using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.Modules.Applications.Application.Applications;

// A candidate's application to a job. The identity fields (email, name) are no longer
// submitted in the form — they come from the authenticated CandidateAccount, fetched via the
// ICandidateAccountReader port so this module never couples to the CandidateAccounts schema.
public sealed record SubmitApplicationCommand(
    string JobSlug,
    Guid CandidateAccountId,
    string? Phone,
    string? LinkedInUrl,
    string? CoverLetter,
    CvUpload? Cv) : ICommand<Guid>;

// A CV attached to this application. Null means "use the one on my account" — the candidate
// uploaded it once from their profile and does not have to find the file again on every job.
// The stream belongs to the request and is only valid for the duration of the call.
public sealed record CvUpload(Stream Content, long SizeBytes, string ContentType, string FileName);

public sealed class SubmitApplicationValidator : AbstractValidator<SubmitApplicationCommand>
{
    // Phone numbers arrive pasted in every format ("+90 (555) 111-22-33", "0555.111.2233"),
    // so the shape check only constrains the character set; plausibility is the digit count.
    // ITU E.164 caps subscriber numbers at 15 digits; 7 is the shortest national number in use.
    private const string PhoneAllowedCharactersPattern = @"^\+?[\d\s().-]+$";
    private const int PhoneMinDigits = 7;
    private const int PhoneMaxDigits = 15;

    public SubmitApplicationValidator()
    {
        RuleFor(x => x.CandidateAccountId).NotEmpty();

        RuleFor(x => x.Phone)
            .MaximumLength(40)
            .Matches(PhoneAllowedCharactersPattern)
                .WithMessage("Phone may only contain digits, spaces, and ()+.- separators.")
            .Must(HaveAPlausibleDigitCount!)
                .WithMessage($"Phone must contain {PhoneMinDigits} to {PhoneMaxDigits} digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.LinkedInUrl)
            .MaximumLength(300)
            .Must(BeAnAbsoluteHttpUrl!)
                .WithMessage("LinkedIn must be a full http(s) URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.LinkedInUrl));

        RuleFor(x => x.CoverLetter).MaximumLength(5000);
    }

    private static bool HaveAPlausibleDigitCount(string phone)
    {
        var digits = phone.Count(char.IsDigit);
        return digits is >= PhoneMinDigits and <= PhoneMaxDigits;
    }

    private static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public sealed class SubmitApplicationHandler : ICommandHandler<SubmitApplicationCommand, Guid>
{
    private readonly IApplicationsDbContext _db;
    private readonly IJobDirectory _jobs;
    private readonly ICandidateAccountReader _candidateAccounts;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentTenant _currentTenant;
    private readonly IPublisher _publisher;
    private readonly IActivityLogRepository _activityLog;
    private readonly ILogger<SubmitApplicationHandler> _logger;

    public SubmitApplicationHandler(
        IApplicationsDbContext db,
        IJobDirectory jobs,
        ICandidateAccountReader candidateAccounts,
        IFileStorage fileStorage,
        ICurrentTenant currentTenant,
        IPublisher publisher,
        IActivityLogRepository activityLog,
        ILogger<SubmitApplicationHandler> logger)
    {
        _db = db;
        _jobs = jobs;
        _candidateAccounts = candidateAccounts;
        _fileStorage = fileStorage;
        _currentTenant = currentTenant;
        _publisher = publisher;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(SubmitApplicationCommand command, CancellationToken ct)
    {
        // The whole flow is tenant-scoped. If the slug did not resolve to a tenant there is no
        // safe way to stamp or query rows, so fail fast.
        if (!_currentTenant.TenantId.HasValue)
            return Result.Failure<Guid>(ApplicationErrors.TenantNotResolved);

        var tenantId = _currentTenant.TenantId.Value;

        // 1. Resolve the candidate identity from the global account. A valid CandidateOnly token
        //    guarantees the account existed at token mint; returning null here would mean it was
        //    deleted in the interim, which we treat as a bad request.
        var account = await _candidateAccounts.GetByIdAsync(command.CandidateAccountId, ct);
        if (account is null)
            return Result.Failure<Guid>(ApplicationErrors.CandidateAccountNotFound);

        //    Applying is the one action that waits for a proven email address. Everything a candidate
        //    does before this point only affects their own account; from here on a recruiter reads the
        //    address, writes to it, and schedules time around it. Checked before the CV is uploaded so
        //    a refused application leaves nothing behind in object storage.
        if (!account.IsEmailVerified)
            return Result.Failure<Guid>(ApplicationErrors.EmailNotVerified);

        // 2. Confirm the job exists and is Published — a cross-module read through the
        //    IJobDirectory port. Applications never sees the Jobs schema or entity.
        var job = await _jobs.GetPublishedJobBySlugAsync(command.JobSlug, ct);
        if (job is null)
            return Result.Failure<Guid>(ApplicationErrors.JobNotAvailable);

        // 3. Deduplicate the per-tenant candidate by email. The tenant half of the (tenant, email)
        //    key is applied automatically by the global query filter. Phone/LinkedIn come from the
        //    form: they can differ per-tenant and are not stored on the global account.
        var email = Candidate.NormalizeEmail(account.Email);
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.Email == email, ct);
        if (candidate is null)
        {
            candidate = Candidate.Create(
                account.Email, account.FirstName, account.LastName,
                command.Phone, command.LinkedInUrl);
            _db.Candidates.Add(candidate);
        }
        else
        {
            // 4. One active application per (candidate, job). A brand-new candidate cannot have
            //    a prior application, so this check only matters for a returning candidate.
            var alreadyApplied = await _db.Applications.AnyAsync(
                a => a.JobId == job.Id
                     && a.CandidateId == candidate.Id
                     && a.Status == ApplicationStatus.Active,
                ct);
            if (alreadyApplied)
                return Result.Failure<Guid>(ApplicationErrors.DuplicateApplication);
        }

        // 5. Materialise the job's pipeline lazily on first application (custom editors are V2).
        //    The unique (tenant, job) index makes this at-most-one per job.
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages)
            .FirstOrDefaultAsync(p => p.JobId == job.Id, ct);
        if (pipeline is null)
        {
            pipeline = Pipeline.CreateDefault(job.Id);
            _db.Pipelines.Add(pipeline);
        }
        var initialStageId = pipeline.InitialStage.Id;

        // 6. Put the CV in storage before persisting. The bucket is private; the file is only ever
        //    reachable through a short-lived presigned URL. The key is grouped under the
        //    candidate (known here) — the application id is generated inside Application.Create,
        //    so using it would force the entity to surrender id generation to this layer.
        var cvKeyResult = await ResolveCvKeyAsync(command, account, tenantId, candidate.Id, ct);
        if (cvKeyResult.IsFailure)
            return Result.Failure<Guid>(cvKeyResult.Error);

        var cvKey = cvKeyResult.Value;

        var application = ApplicationEntity.Create(
            job.Id, candidate.Id, command.CandidateAccountId, initialStageId, cvKey, command.CoverLetter);
        _db.Applications.Add(application);

        // Publish before saving: with the transactional outbox, the bridge handler writes the
        // integration event to the outbox tables in this same DbContext, so it commits atomically
        // with the rows below — a broker outage can no longer lose it or block this request.
        await _publisher.Publish(
            new ApplicationSubmittedEvent(
                application.Id, job.Id, job.Title, candidate.Id,
                candidate.Email, candidate.FirstName, candidate.LastName, tenantId),
            ct);

        // Also request CV parsing. Like the event above, this is bridged onto RabbitMQ via the
        // outbox, so it commits in the same transaction as the application row: a parse can never be
        // requested for an application that was not saved, and vice versa.
        await _publisher.Publish(
            new CvParseRequestedEvent(application.Id, job.Id, candidate.Id, cvKey, tenantId),
            ct);

        try
        {
            // Candidate (if new), pipeline (if new), the application and the outbox message commit
            // in one transaction — SaveChanges is atomic per DbContext.
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // The upload is not part of the DB transaction. If persistence fails, delete the
            // orphaned object so storage does not accumulate unreferenced CVs (a PII concern).
            await TryDeleteAsync(cvKey, ct);
            throw;
        }

        // Record the first entry in the application's history, now in MongoDB. This is not part of
        // the transaction above (Mongo is a separate system): the application is committed first,
        // then logged best-effort.
        await _activityLog.TryAddAsync(
            ApplicationActivity.Submitted(application.Id, job.Id, candidate.Email), _logger, ct);

        return Result.Success(application.Id);
    }

    // Produces the key of the CV this application will own. Either way the result is an object of
    // this application's own, under this tenant's prefix — never a pointer into the candidate's
    // account. An application is a record of what was sent on the day it was sent: the candidate
    // may replace or erase their account CV afterwards, and the recruiter must still see the
    // document they were given.
    private async Task<Result<string>> ResolveCvKeyAsync(
        SubmitApplicationCommand command,
        CandidateAccountSummary account,
        Guid tenantId,
        Guid candidateId,
        CancellationToken ct)
    {
        if (command.Cv is { } upload)
        {
            var uploadedKey = BuildCvKey(tenantId, candidateId, upload.FileName);
            await _fileStorage.UploadAsync(
                uploadedKey, upload.Content, upload.SizeBytes, upload.ContentType, ct);

            return Result.Success(uploadedKey);
        }

        // Nothing attached and nothing on file: the form let them through because the CV field is
        // optional, so this is where it is caught.
        if (account.CvFileKey is null || account.CvFileName is null)
            return Result.Failure<string>(ApplicationErrors.CvRequired);

        var copiedKey = BuildCvKey(tenantId, candidateId, account.CvFileName);
        await _fileStorage.CopyAsync(account.CvFileKey, copiedKey, ct);

        return Result.Success(copiedKey);
    }

    private static string BuildCvKey(Guid tenantId, Guid candidateId, string fileName) =>
        $"{tenantId}/{candidateId}/{Guid.NewGuid()}-{StorageKey.SanitizeFileName(fileName)}";

    private async Task TryDeleteAsync(string key, CancellationToken ct)
    {
        try
        {
            await _fileStorage.DeleteAsync(key, ct);
        }
        catch
        {
            // Best-effort compensation: swallow so the original persistence failure surfaces.
        }
    }
}
