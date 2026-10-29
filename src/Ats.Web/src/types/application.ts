import type {
  ApplicationActivityType,
  ApplicationStatus,
  CvJobFitRating,
  PipelineStageType,
} from './enums';

/* The job is only an id; the screen resolves titles from the jobs it loads. */
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

/* `type` decides which stages may be offered as move targets. */
export interface PipelineStage {
  id: string;
  name: string;
  order: number;
  type: PipelineStageType;
}

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

/* Produced asynchronously; 404 "application.cv_not_parsed" until done. The fit fields compare the CV
   with this job and only list concrete skills. */
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

/* The payload shape depends on activityType. */
export interface ApplicationActivity {
  id: string;
  activityType: ApplicationActivityType;
  actorUserId: string | null;
  payload: Record<string, unknown>;
  occurredAtUtc: string;
}
