using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Kernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// Alias because "Application" is also this namespace.
using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.Modules.Applications.Application.Applications;

// Email and name come from the candidate account through ICandidateAccountReader.
public sealed record SubmitApplicationCommand(
    string JobSlug,
    Guid CandidateAccountId,
    string? Phone,
    string? LinkedInUrl,
    string? CoverLetter,
    CvUpload? Cv) : ICommand<Guid>;

// Null means use the CV on the account. The stream is only valid during the call.
public sealed record CvUpload(Stream Content, long SizeBytes, string ContentType, string FileName);

public sealed class SubmitApplicationValidator : AbstractValidator<SubmitApplicationCommand>
{
    // Pasted numbers come in many formats, so only the character set is checked here; the digit
    // count (7 to 15, E.164) is the real check.
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
        if (!_currentTenant.TenantId.HasValue)
            return Result.Failure<Guid>(ApplicationErrors.TenantNotResolved);

        var tenantId = _currentTenant.TenantId.Value;

        // The token was valid when issued; a missing account means it was deleted since.
        var account = await _candidateAccounts.GetByIdAsync(command.CandidateAccountId, ct);
        if (account is null)
            return Result.Failure<Guid>(ApplicationErrors.CandidateAccountNotFound);

        // A frozen account keeps its session, so check it here. Before the email check.
        if (!account.IsActive)
            return Result.Failure<Guid>(ApplicationErrors.AccountFrozen);

        // Applying is the only action that needs a verified email. Checked before the CV upload so a
        // refusal leaves nothing in storage.
        if (!account.IsEmailVerified)
            return Result.Failure<Guid>(ApplicationErrors.EmailNotVerified);

        // The job must exist and be published.
        var job = await _jobs.GetPublishedJobBySlugAsync(command.JobSlug, ct);
        if (job is null)
            return Result.Failure<Guid>(ApplicationErrors.JobNotAvailable);

        // Phone and LinkedIn come from the form; they aren't stored on the global account.
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
            // Friendly pre-check only. Concurrent submits can both pass it; the partial unique index is the
            // real guard and the catch below maps its failure to the same error.
            if (await HasActiveApplicationAsync(job.Id, candidate.Id, ct))
                return Result.Failure<Guid>(ApplicationErrors.DuplicateApplication);

            // After the duplicate check, so a refused application changes nothing.
            candidate.UpdateContactDetails(command.Phone, command.LinkedInUrl);
        }

        // Created on the first application. The (tenant, job) unique index keeps it to one.
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages)
            .FirstOrDefaultAsync(p => p.JobId == job.Id, ct);
        if (pipeline is null)
        {
            pipeline = Pipeline.CreateDefault(job.Id);
            _db.Pipelines.Add(pipeline);
        }
        var initialStageId = pipeline.InitialStage.Id;

        // Stored under the candidate; the application id isn't known until Application.Create.
        var cvKeyResult = await ResolveCvKeyAsync(command, account, tenantId, candidate.Id, ct);
        if (cvKeyResult.IsFailure)
            return Result.Failure<Guid>(cvKeyResult.Error);

        var cvKey = cvKeyResult.Value;

        var application = ApplicationEntity.Create(
            job.Id, candidate.Id, command.CandidateAccountId, initialStageId, cvKey, command.CoverLetter);
        _db.Applications.Add(application);

        // Published before saving so the outbox message commits with the application.
        await _publisher.Publish(
            new ApplicationSubmittedEvent(
                application.Id, job.Id, job.Title, candidate.Id,
                candidate.Email, candidate.FirstName, candidate.LastName, tenantId),
            ct);

        // Also through the outbox, so a parse is only requested for a saved application.
        await _publisher.Publish(
            new CvParseRequestedEvent(application.Id, job.Id, candidate.Id, cvKey, tenantId),
            ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception exception)
        {
            // Not part of the transaction, so delete the uploaded CV if the save fails.
            await TryDeleteAsync(cvKey, ct);

            // Ask the database the same question as the pre-check instead of decoding a driver error. Any
            // other failure (e.g. the candidate's own email index) is real and propagates.
            if (exception is DbUpdateException
                && await HasActiveApplicationAsync(job.Id, candidate.Id, ct))
            {
                return Result.Failure<Guid>(ApplicationErrors.DuplicateApplication);
            }

            throw;
        }

        // After the save; the activity log is in MongoDB, outside the transaction.
        await _activityLog.TryAddAsync(
            ApplicationActivity.Submitted(application.Id, job.Id, candidate.Email), _logger, ct);

        return Result.Success(application.Id);
    }

    // Shared by the pre-check and the post-failure check so the two can't drift.
    private Task<bool> HasActiveApplicationAsync(Guid jobId, Guid candidateId, CancellationToken ct) =>
        _db.Applications.AnyAsync(
            a => a.JobId == jobId
                 && a.CandidateId == candidateId
                 && a.Status == ApplicationStatus.Active,
            ct);

    // Always a copy under this tenant, never a pointer to the account CV: the candidate may replace
    // or delete that later, and the recruiter must keep what was sent.
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

        // The CV field is optional, so this is where a missing CV is caught.
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
            // Best-effort; let the original failure surface.
        }
    }
}
