import type { EmploymentType, ExperienceLevel, JobStatus, WorkArrangement } from './enums';

/* The list DTO has no description or salary; those come with the detail. */
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

/* PublishedAtUtc is null for drafts. */
export interface JobDetail extends Job {
  description: string;
  salaryMin: number | null;
  salaryMax: number | null;
  salaryCurrency: string | null;
  publishedAtUtc: string | null;
}

/* Used for both create and update. CreatedBy comes from the token. */
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
