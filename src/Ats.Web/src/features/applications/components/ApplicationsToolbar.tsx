import { useTranslation } from 'react-i18next';
import { Input, Select } from '@/components/ui';
import { APPLICATION_STATUSES, type ApplicationStatus } from '@/types/enums';
import type { Job } from '@/types/job';
import type { PipelineStage } from '@/types/application';

interface ApplicationsToolbarProps {
  searchValue: string;
  onSearchChange: (value: string) => void;
  jobId: string;
  onJobChange: (value: string) => void;
  stageId: string;
  onStageChange: (value: string) => void;
  status: ApplicationStatus | '';
  onStatusChange: (value: ApplicationStatus | '') => void;
  jobs: Job[];
  stages: PipelineStage[];
  /** Stages are per-job, so the stage filter is only usable once a job is selected. */
  stagesEnabled: boolean;
}

function SearchIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="11" cy="11" r="8" />
      <path d="m21 21-4.3-4.3" />
    </svg>
  );
}

/* Filter bar: candidate search plus job, stage, and status selects. Fully controlled — the page owns
   the values and mirrors them to the URL. */
export function ApplicationsToolbar({
  searchValue,
  onSearchChange,
  jobId,
  onJobChange,
  stageId,
  onStageChange,
  status,
  onStatusChange,
  jobs,
  stages,
  stagesEnabled,
}: ApplicationsToolbarProps) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center">
      <div className="relative flex-1 sm:max-w-xs">
        <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-text-muted">
          <SearchIcon />
        </span>
        <Input
          type="search"
          aria-label={t('applications.searchPlaceholder')}
          placeholder={t('applications.searchPlaceholder')}
          value={searchValue}
          onChange={(event) => onSearchChange(event.target.value)}
          className="pl-9"
        />
      </div>

      <Select
        aria-label={t('applications.filterJob')}
        value={jobId}
        onChange={(event) => onJobChange(event.target.value)}
        className="sm:w-48"
      >
        <option value="">{t('applications.allJobs')}</option>
        {jobs.map((job) => (
          <option key={job.id} value={job.id}>
            {job.title}
          </option>
        ))}
      </Select>

      <Select
        aria-label={t('applications.filterStage')}
        value={stageId}
        onChange={(event) => onStageChange(event.target.value)}
        disabled={!stagesEnabled}
        className="sm:w-44"
      >
        <option value="">{t('applications.allStages')}</option>
        {stages.map((stage) => (
          <option key={stage.id} value={stage.id}>
            {stage.name}
          </option>
        ))}
      </Select>

      <Select
        aria-label={t('applications.filterStatus')}
        value={status}
        onChange={(event) => onStatusChange(event.target.value as ApplicationStatus | '')}
        className="sm:w-40"
      >
        <option value="">{t('applications.allStatuses')}</option>
        {APPLICATION_STATUSES.map((value) => (
          <option key={value} value={value}>
            {t(`status.${value}`)}
          </option>
        ))}
      </Select>
    </div>
  );
}
