/* Mirrors the backend enums, which are serialized as names. Each is a const array plus a derived
   union, so it works both as a runtime list and as a type. */

export const JOB_STATUSES = ['Draft', 'Published', 'Closed', 'Archived'] as const;
export type JobStatus = (typeof JOB_STATUSES)[number];

export const EMPLOYMENT_TYPES = ['FullTime', 'PartTime', 'Contract', 'Internship'] as const;
export type EmploymentType = (typeof EMPLOYMENT_TYPES)[number];

export const EXPERIENCE_LEVELS = ['Junior', 'Mid', 'Senior', 'Lead'] as const;
export type ExperienceLevel = (typeof EXPERIENCE_LEVELS)[number];

export const APPLICATION_STATUSES = ['Active', 'Withdrawn', 'Rejected', 'Hired'] as const;
export type ApplicationStatus = (typeof APPLICATION_STATUSES)[number];

/* Mirrors SupportedCurrencies (strings on the backend too). */
export const CURRENCIES = ['TRY', 'USD', 'EUR', 'GBP'] as const;
export type Currency = (typeof CURRENCIES)[number];

export const WORK_ARRANGEMENTS = ['Remote', 'Hybrid', 'OnSite'] as const;
export type WorkArrangement = (typeof WORK_ARRANGEMENTS)[number];

export const PIPELINE_STAGE_TYPES = ['Initial', 'Active', 'Interview', 'FinalHired', 'FinalRejected'] as const;
export type PipelineStageType = (typeof PIPELINE_STAGE_TYPES)[number];

export const APPLICATION_ACTIVITY_TYPES =
  ['Submitted', 'Viewed', 'StageChanged', 'StageCorrected', 'Rejected', 'Hired', 'Withdrawn'] as const;
export type ApplicationActivityType = (typeof APPLICATION_ACTIVITY_TYPES)[number];

export const INTERVIEW_TYPES = ['PhoneScreen', 'Technical', 'Cultural', 'Final'] as const;
export type InterviewType = (typeof INTERVIEW_TYPES)[number];

export const INTERVIEW_STATUSES = ['Scheduled', 'Completed', 'Cancelled', 'NoShow'] as const;
export type InterviewStatus = (typeof INTERVIEW_STATUSES)[number];

/* Mirrors Interview.AllowedDurationMinutes. */
export const INTERVIEW_DURATION_OPTIONS = [10, 15, 20, 30, 45, 60] as const;
export const DEFAULT_INTERVIEW_DURATION = 30;

export const FEEDBACK_RECOMMENDATIONS = ['StrongNoHire', 'NoHire', 'Hire', 'StrongHire'] as const;
export type FeedbackRecommendation = (typeof FEEDBACK_RECOMMENDATIONS)[number];

export const ROLES = ['Admin', 'Recruiter', 'HiringManager', 'ReadOnly'] as const;
export type Role = (typeof ROLES)[number];

/* Qualitative, never a numeric score. */
export const CV_JOB_FIT_RATINGS = ['Weak', 'Moderate', 'Strong'] as const;
export type CvJobFitRating = (typeof CV_JOB_FIT_RATINGS)[number];
