import type {
  ApplicationActivityType,
  ApplicationStatus,
  CvJobFitRating,
  PipelineStageType,
} from './enums';

/* Recruiter list row (Applications.Application.ApplicationListItemDto). The candidate name/email and
   the current stage name are joined server-side; the job is referenced by id only (the screen
   resolves the title from the jobs it loads for the filter). */
export interface ApplicationListItem {
  id: string;
  candidateName: string;
  candidateEmail: string;
  jobId: string;
  stageId: string;
  stageName: string;
  status: ApplicationStatus;
  appliedAtUtc: string;
}

/* A stage of a job's pipeline (Applications.Application.PipelineStageDto), from
   GET /jobs/{jobId}/stages. Drives the stage filter, the Kanban columns, and — via type — which
   stages the move-stage UI may offer (terminal stages are outcomes, not move targets). */
export interface PipelineStage {
  id: string;
  name: string;
  order: number;
  type: PipelineStageType;
}

/* GET /api/v1/applications/{id} (Applications.Application.ApplicationDetailDto). The detail screen's
   header and tabs read from this; the CV/parse/activity tabs load from their own endpoints. */
export interface ApplicationDetail {
  id: string;
  jobId: string;
  candidateId: string;
  candidateName: string;
  candidateEmail: string;
  phone: string | null;
  linkedInUrl: string | null;
  stageId: string;
  stageName: string;
  status: ApplicationStatus;
  coverLetter: string | null;
  rejectionReason: string | null;
  hasCv: boolean;
  appliedAtUtc: string;
}

/* GET /api/v1/applications/{id}/cv-download-url — a short-lived presigned link to the CV file. */
export interface CvDownloadUrl {
  url: string;
  expiresInSeconds: number;
}

export interface CvEducation {
  degree: string;
  institution: string;
  year: number;
}

export interface CvPosition {
  title: string;
  company: string;
  startDate: string;
  endDate: string;
}

/* GET /api/v1/applications/{id}/cv-parse-result — produced asynchronously, so a fresh application may
   404 (code "application.cv_not_parsed") until parsing finishes. jobFitRating/fitSummary/
   matchedRequirements/missingRequirements are the CV judged against the specific job it was
   submitted for; matchedRequirements/missingRequirements are limited to concrete technical
   skills the job description names -- never career-gap or tenure-pattern inferences. */
export interface CvParseResult {
  applicationId: string;
  skills: string[];
  totalExperienceYears: number;
  education: CvEducation[];
  recentPositions: CvPosition[];
  jobFitRating: CvJobFitRating;
  fitSummary: string;
  matchedRequirements: string[];
  missingRequirements: string[];
  parsedAtUtc: string;
}

/* GET /api/v1/applications/{id}/activities — the append-only timeline. The payload shape depends on
   activityType: Submitted { jobId, candidateEmail }, Viewed {}, StageChanged { fromStageId, toStageId },
   Rejected { reason }. */
export interface ApplicationActivity {
  id: string;
  activityType: ApplicationActivityType;
  actorUserId: string | null;
  payload: Record<string, unknown>;
  occurredAtUtc: string;
}
