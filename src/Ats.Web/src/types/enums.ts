/* Enum unions mirrored 1:1 from the backend (verified against the *Enums.cs files).
   The API serializes enums as their string name (JsonStringEnumConverter), so these are string
   unions, not numbers. Each is declared as a `const` array first and the union is derived from it,
   so we get a single source of truth that doubles as a runtime list (for selects, filters, and
   i18n label maps) and a compile-time type. */

export const JOB_STATUSES = ['Draft', 'Published', 'Closed', 'Archived'] as const;
export type JobStatus = (typeof JOB_STATUSES)[number];

export const EMPLOYMENT_TYPES = ['FullTime', 'PartTime', 'Contract', 'Internship'] as const;
export type EmploymentType = (typeof EMPLOYMENT_TYPES)[number];

export const EXPERIENCE_LEVELS = ['Junior', 'Mid', 'Senior', 'Lead'] as const;
export type ExperienceLevel = (typeof EXPERIENCE_LEVELS)[number];

export const APPLICATION_STATUSES = ['Active', 'Withdrawn', 'Rejected', 'Hired'] as const;
export type ApplicationStatus = (typeof APPLICATION_STATUSES)[number];

export const PIPELINE_STAGE_TYPES = ['Initial', 'Active', 'FinalHired', 'FinalRejected'] as const;
export type PipelineStageType = (typeof PIPELINE_STAGE_TYPES)[number];

export const APPLICATION_ACTIVITY_TYPES = ['Submitted', 'StageChanged', 'Rejected'] as const;
export type ApplicationActivityType = (typeof APPLICATION_ACTIVITY_TYPES)[number];

export const INTERVIEW_TYPES = ['PhoneScreen', 'Technical', 'Cultural', 'Final'] as const;
export type InterviewType = (typeof INTERVIEW_TYPES)[number];

export const INTERVIEW_STATUSES = ['Scheduled', 'Completed', 'Cancelled', 'NoShow'] as const;
export type InterviewStatus = (typeof INTERVIEW_STATUSES)[number];

export const FEEDBACK_RECOMMENDATIONS = ['StrongNoHire', 'NoHire', 'Hire', 'StrongHire'] as const;
export type FeedbackRecommendation = (typeof FEEDBACK_RECOMMENDATIONS)[number];

export const ROLES = ['Admin', 'Recruiter', 'HiringManager', 'ReadOnly'] as const;
export type Role = (typeof ROLES)[number];
