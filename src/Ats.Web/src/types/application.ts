import type { ApplicationStatus } from './enums';

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
   GET /jobs/{jobId}/stages. Drives the stage filter here and the Kanban columns later. */
export interface PipelineStage {
  id: string;
  name: string;
  order: number;
}
