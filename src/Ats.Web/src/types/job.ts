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

/* GET /api/v1/jobs/{id} (Jobs.Application.JobDetailDto). Adds the fields the edit form prefills that
   the list row omits: the markdown description and the optional salary range. */
export interface JobDetail extends Job {
  description: string;
  salaryMin: number | null;
  salaryMax: number | null;
  salaryCurrency: string | null;
}

/* Request body for POST /jobs and PUT /jobs/{id} (both controllers share this shape). CreatedBy is
   never sent — the backend fills it from the JWT. */
export interface JobWriteRequest {
  title: string;
  description: string;
  department: string;
  location: string;
  employmentType: EmploymentType;
  experienceLevel: ExperienceLevel;
  salaryMin: number | null;
  salaryMax: number | null;
  salaryCurrency: string | null;
}
