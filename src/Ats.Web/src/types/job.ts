import type { EmploymentType, ExperienceLevel, JobStatus, WorkArrangement } from './enums';

/* The recruiter list/detail row (Jobs.Application.JobDto). Enums serialize as their string name and
   timestamps as ISO strings. The list DTO intentionally omits description/salary — those load with
   the detail/edit screen, not the table. City/Country replace the old single free-text Location
   field (backend renamed Location -> City and added the optional Country column). */
export interface Job {
  id: string;
  title: string;
  department: string;
  city: string;
  country: string | null;
  employmentType: EmploymentType;
  experienceLevel: ExperienceLevel;
  workArrangement: WorkArrangement;
  status: JobStatus;
  slug: string;
  createdAtUtc: string;
}

/* GET /api/v1/jobs/{id} (Jobs.Application.JobDetailDto). Adds the fields the edit form prefills that
   the list row omits: the markdown description and the optional salary range. PublishedAtUtc is null
   for drafts — the public detail page shows it as the posting date. */
export interface JobDetail extends Job {
  description: string;
  salaryMin: number | null;
  salaryMax: number | null;
  salaryCurrency: string | null;
  publishedAtUtc: string | null;
}

/* Request body for POST /jobs and PUT /jobs/{id} (both controllers share this shape). CreatedBy is
   never sent — the backend fills it from the JWT. */
export interface JobWriteRequest {
  title: string;
  description: string;
  department: string;
  city: string;
  country: string | null;
  employmentType: EmploymentType;
  experienceLevel: ExperienceLevel;
  workArrangement: WorkArrangement;
  salaryMin: number | null;
  salaryMax: number | null;
  salaryCurrency: string | null;
}
