import { useTranslation } from 'react-i18next';
import { Button, Select } from '@/components/ui';
import { fullName } from '@/features/users/useUsers';
import type { TenantUser } from '@/types/user';
import { DATE_RANGES, type DateRange } from '../dateRange';

interface InterviewsToolbarProps {
  range: DateRange;
  onRangeChange: (value: DateRange) => void;
  interviewerId: string;
  onInterviewerChange: (value: string) => void;
  users: TenantUser[];
  canManage: boolean;
  onSchedule: () => void;
}

function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <line x1="12" y1="5" x2="12" y2="19" />
      <line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  );
}

/* Filter bar for the interviews list: a date-range preset and an interviewer filter, plus the
   schedule action for managers. Fully controlled — the page owns the values and mirrors them to the
   URL. The backend only filters by date range and interviewer, so those are the only filters here. */
export function InterviewsToolbar({
  range,
  onRangeChange,
  interviewerId,
  onInterviewerChange,
  users,
  canManage,
  onSchedule,
}: InterviewsToolbarProps) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center">
      <Select
        aria-label={t('interviews.filterRange')}
        value={range}
        onChange={(event) => onRangeChange(event.target.value as DateRange)}
        className="sm:w-44"
      >
        {DATE_RANGES.map((value) => (
          <option key={value} value={value}>
            {t(`interviews.range.${value}`)}
          </option>
        ))}
      </Select>

      <Select
        aria-label={t('interviews.filterInterviewer')}
        value={interviewerId}
        onChange={(event) => onInterviewerChange(event.target.value)}
        className="sm:w-56"
      >
        <option value="">{t('interviews.allInterviewers')}</option>
        {users.map((user) => (
          <option key={user.id} value={user.id}>
            {fullName(user)}
          </option>
        ))}
      </Select>

      {canManage && (
        <Button leadingIcon={<PlusIcon />} onClick={onSchedule} className="sm:ml-auto">
          {t('interviews.schedule')}
        </Button>
      )}
    </div>
  );
}
