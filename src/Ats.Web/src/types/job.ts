import type { EmploymentType, ExperienceLevel, JobStatus } from './enums';

/* The recruiter list/detail row (Jobs.Application.JobDto). Enums serialize as their string name and
   timestamps as ISO strings. The list DTO intentionally omits description/salary — those load with
   the detail/edit screen, not the table. */
export interface Job {
  id: string;
  title: string;
  department: string;
  location: string;
  employmentType: EmploymentType;
  experienceLevel: ExperienceLevel;
  status: JobStatus;
  slug: string;
  createdAtUtc: string;
}
