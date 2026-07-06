using Ats.Modules.Jobs.Domain;
using Ats.Shared.Kernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Application.Jobs;

public static class JobErrors
{
    public static readonly Error NotFound = new("job.not_found", "Job not found.");

    public static Error InvalidOperation(string message) =>
        new("job.invalid_operation", message);
}

// ---- Publish ----
public sealed record PublishJobCommand(Guid JobId) : ICommand<bool>;

public sealed class PublishJobValidator : AbstractValidator<PublishJobCommand>
{
    public PublishJobValidator() => RuleFor(x => x.JobId).NotEmpty();
}

public sealed class PublishJobHandler : ICommandHandler<PublishJobCommand, bool>
{
    private readonly IJobsDbContext _db;
    public PublishJobHandler(IJobsDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(PublishJobCommand command, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == command.JobId, ct);
        if (job is null)
            return Result.Failure<bool>(JobErrors.NotFound);

        try { job.Publish(); }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(JobErrors.InvalidOperation(ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

// ---- Close ----
public sealed record CloseJobCommand(Guid JobId) : ICommand<bool>;

public sealed class CloseJobValidator : AbstractValidator<CloseJobCommand>
{
    public CloseJobValidator() => RuleFor(x => x.JobId).NotEmpty();
}

public sealed class CloseJobHandler : ICommandHandler<CloseJobCommand, bool>
{
    private readonly IJobsDbContext _db;
    public CloseJobHandler(IJobsDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(CloseJobCommand command, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == command.JobId, ct);
        if (job is null)
            return Result.Failure<bool>(JobErrors.NotFound);

        try { job.Close(); }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(JobErrors.InvalidOperation(ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

// ---- Archive ----
public sealed record ArchiveJobCommand(Guid JobId) : ICommand<bool>;

public sealed class ArchiveJobValidator : AbstractValidator<ArchiveJobCommand>
{
    public ArchiveJobValidator() => RuleFor(x => x.JobId).NotEmpty();
}

public sealed class ArchiveJobHandler : ICommandHandler<ArchiveJobCommand, bool>
{
    private readonly IJobsDbContext _db;
    public ArchiveJobHandler(IJobsDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(ArchiveJobCommand command, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == command.JobId, ct);
        if (job is null)
            return Result.Failure<bool>(JobErrors.NotFound);

        try { job.Archive(); }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(JobErrors.InvalidOperation(ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

// ---- Update ----
public sealed record UpdateJobCommand(
    Guid JobId,
    string Title,
    string Description,
    string Department,
    string City,
    string? Country,
    EmploymentType EmploymentType,
    ExperienceLevel ExperienceLevel,
    WorkArrangement WorkArrangement,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency) : ICommand<bool>;

public sealed class UpdateJobValidator : AbstractValidator<UpdateJobCommand>
{
    public UpdateJobValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.SalaryCurrency)
            .Must(currency => currency is not null && SupportedCurrencies.All.Contains(currency.ToUpperInvariant()))
            .When(x => x.SalaryMin.HasValue || x.SalaryMax.HasValue)
            .WithMessage($"Currency must be one of: {string.Join(", ", SupportedCurrencies.All)}.");
    }
}

public sealed class UpdateJobHandler : ICommandHandler<UpdateJobCommand, bool>
{
    private readonly IJobsDbContext _db;
    public UpdateJobHandler(IJobsDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateJobCommand command, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == command.JobId, ct);
        if (job is null)
            return Result.Failure<bool>(JobErrors.NotFound);

        SalaryRange? salary = null;
        if (command.SalaryMin.HasValue && command.SalaryMax.HasValue && command.SalaryCurrency is not null)
            salary = new SalaryRange(command.SalaryMin.Value, command.SalaryMax.Value, command.SalaryCurrency);

        try
        {
            job.UpdateDetails(
                command.Title, command.Description, command.Department, command.City, command.Country,
                command.EmploymentType, command.ExperienceLevel, command.WorkArrangement, salary);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(JobErrors.InvalidOperation(ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
