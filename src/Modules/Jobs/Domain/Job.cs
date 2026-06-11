using Ats.Shared.Kernel;

namespace Ats.Modules.Jobs.Domain;

public sealed class Job : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string Department { get; private set; } = null!;
    public string Location { get; private set; } = null!;
    public EmploymentType EmploymentType { get; private set; }
    public ExperienceLevel ExperienceLevel { get; private set; }
    public SalaryRange? SalaryRange { get; private set; }
    public JobStatus Status { get; private set; }
    public string Slug { get; private set; } = null!;
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Job(
        Guid id, string title, string description, string department, string location,
        EmploymentType employmentType, ExperienceLevel experienceLevel, SalaryRange? salaryRange,
        string slug, Guid createdBy)
    {
        Id = id;
        Title = title;
        Description = description;
        Department = department;
        Location = location;
        EmploymentType = employmentType;
        ExperienceLevel = experienceLevel;
        SalaryRange = salaryRange;
        Slug = slug;
        Status = JobStatus.Draft;
        CreatedBy = createdBy;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private Job() { }

    public static Job Create(
        string title, string description, string department, string location,
        EmploymentType employmentType, ExperienceLevel experienceLevel,
        SalaryRange? salaryRange, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var slug = GenerateSlug(title);

        return new Job(
            Guid.NewGuid(), title, description ?? string.Empty, department ?? string.Empty,
            location ?? string.Empty, employmentType, experienceLevel, salaryRange, slug, createdBy);
    }

    public void Publish()
    {
        if (Status != JobStatus.Draft)
            throw new InvalidOperationException("Only a draft job can be published.");
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Description))
            throw new InvalidOperationException("Title and description are required to publish.");

        Status = JobStatus.Published;
        PublishedAtUtc = DateTime.UtcNow;
    }

    public void Close()
    {
        if (Status != JobStatus.Published)
            throw new InvalidOperationException("Only a published job can be closed.");

        Status = JobStatus.Closed;
        ClosedAtUtc = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status == JobStatus.Archived)
            throw new InvalidOperationException("Job is already archived.");

        Status = JobStatus.Archived;
    }

    public void UpdateDetails(
        string title, string description, string department, string location,
        EmploymentType employmentType, ExperienceLevel experienceLevel, SalaryRange? salaryRange)
    {
        if (Status == JobStatus.Archived)
            throw new InvalidOperationException("An archived job cannot be edited.");

        Title = string.IsNullOrWhiteSpace(title) ? Title : title;
        Description = description ?? Description;
        Department = department ?? Department;
        Location = location ?? Location;
        EmploymentType = employmentType;
        ExperienceLevel = experienceLevel;
        SalaryRange = salaryRange;
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.Trim().ToLowerInvariant().Replace(' ', '-');
        var clean = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        return $"{clean}-{Guid.NewGuid().ToString()[..8]}";
    }
}
