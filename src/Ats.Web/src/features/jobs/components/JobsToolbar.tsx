import { useTranslation } from 'react-i18next';
import { Button, Input, Select } from '@/components/ui';
import { CareersPageLink } from '@/components/CareersPageLink';
import { JOB_STATUSES, type JobStatus } from '@/types/enums';

interface JobsToolbarProps {
  searchValue: string;
  onSearchChange: (value: string) => void;
  status: JobStatus | '';
  onStatusChange: (value: JobStatus | '') => void;
  canManage: boolean;
  onNewJob: () => void;
  /** Tenant slug for the "View careers page" link; omitted if the identity has no slug. */
  careersSlug?: string;
}

function SearchIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="11" cy="11" r="8" />
      <path d="m21 21-4.3-4.3" />
    </svg>
  );
}

function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M12 5v14M5 12h14" />
    </svg>
  );
}

/* Filter bar above the table: title search, status filter, and (for managers) the New job button.
   Fully controlled — the page owns the values and reflects them to the URL. */
export function JobsToolbar({
  searchValue,
  onSearchChange,
  status,
  onStatusChange,
  canManage,
  onNewJob,
  careersSlug,
}: JobsToolbarProps) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
      <div className="relative flex-1 sm:max-w-xs">
        <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-text-muted">
          <SearchIcon />
        </span>
        <Input
          type="search"
          aria-label={t('jobs.searchPlaceholder')}
          placeholder={t('jobs.searchPlaceholder')}
          value={searchValue}
          onChange={(event) => onSearchChange(event.target.value)}
          className="pl-9"
        />
      </div>

      <Select
        aria-label={t('jobs.filterStatus')}
        value={status}
        onChange={(event) => onStatusChange(event.target.value as JobStatus | '')}
        className="sm:w-44"
      >
        <option value="">{t('jobs.allStatuses')}</option>
        {JOB_STATUSES.map((value) => (
          <option key={value} value={value}>
            {t(`status.${value}`)}
          </option>
        ))}
      </Select>

      <div className="flex items-center gap-3 sm:ml-auto">
        {careersSlug && <CareersPageLink slug={careersSlug} />}
        {canManage && (
          <Button leadingIcon={<PlusIcon />} onClick={onNewJob}>
            {t('jobs.newJob')}
          </Button>
        )}
      </div>
    </div>
  );
}
