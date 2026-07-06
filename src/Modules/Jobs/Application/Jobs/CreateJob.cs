using Ats.Modules.Jobs.Domain;
using Ats.Shared.Kernel;
using FluentValidation;

namespace Ats.Modules.Jobs.Application.Jobs;

public sealed record CreateJobCommand(
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
    string? SalaryCurrency,
    Guid CreatedBy) : ICommand<Guid>;

public sealed class CreateJobValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.SalaryCurrency)
            .Must(currency => currency is not null && SupportedCurrencies.All.Contains(currency.ToUpperInvariant()))
            .When(x => x.SalaryMin.HasValue || x.SalaryMax.HasValue)
            .WithMessage($"Currency must be one of: {string.Join(", ", SupportedCurrencies.All)}.");
    }
}

public sealed class CreateJobHandler : ICommandHandler<CreateJobCommand, Guid>
{
    private readonly IJobsDbContext _db;

    public CreateJobHandler(IJobsDbContext db)
    {
        _db = db;
    }

    public async Task<Result<Guid>> Handle(CreateJobCommand command, CancellationToken cancellationToken)
    {
        SalaryRange? salary = null;
        if (command.SalaryMin.HasValue && command.SalaryMax.HasValue && command.SalaryCurrency is not null)
            salary = new SalaryRange(command.SalaryMin.Value, command.SalaryMax.Value, command.SalaryCurrency);

        var job = Job.Create(
            command.Title, command.Description, command.Department, command.City, command.Country,
            command.EmploymentType, command.ExperienceLevel, command.WorkArrangement, salary, command.CreatedBy);

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(job.Id);
    }
}
