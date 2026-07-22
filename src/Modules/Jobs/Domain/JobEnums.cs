namespace Ats.Modules.Jobs.Domain;

public enum EmploymentType { FullTime, PartTime, Contract, Internship }

public enum ExperienceLevel { Junior, Mid, Senior, Lead }

public enum JobStatus { Draft, Published, Closed, Archived }

// Brand-new column (unlike SalaryRange.Currency, which stays a plain string because it retrofits an
// existing free-text column with unbounded legacy history). Since no row has a value yet, the
// migration backfills every existing job with OnSite, so this can safely be a real enum from day one.
public enum WorkArrangement { Remote, Hybrid, OnSite }
