import { useTranslation } from 'react-i18next';
import { Select } from '@/components/ui';
import { fullName } from '@/features/users/useUsers';
import type { TenantUser } from '@/types/user';
import { INTERVIEW_LIST_FILTERS, type InterviewListFilter } from '@/types/interview';
import { DATE_RANGES, type DateRange } from '../dateRange';

interface InterviewsToolbarProps {
  range: DateRange;
  onRangeChange: (value: DateRange) => void;
  interviewerId: string;
  onInterviewerChange: (value: string) => void;
  filter: InterviewListFilter | '';
  onFilterChange: (value: InterviewListFilter | '') => void;
  users: TenantUser[];
}

/* Filter bar for the interviews list: a date-range preset and an interviewer filter. Fully
   controlled — the page owns the values and mirrors them to the URL. Scheduling lives on the
   application, not here: interviews are created from a candidate's pipeline, so this screen only
   reads and filters. */
export function InterviewsToolbar({
  range,
  onRangeChange,
  interviewerId,
  onInterviewerChange,
  filter,
  onFilterChange,
  users,
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

      {/* Lifecycle bucket. "AwaitingOutcome" is the one this screen was missing: without it there
          was no way to ask which interviews have happened but never got a decision. */}
      <Select
        aria-label={t('interviews.filterState')}
        value={filter}
        onChange={(event) => onFilterChange(event.target.value as InterviewListFilter | '')}
        className="sm:w-48"
      >
        <option value="">{t('interviews.allStates')}</option>
        {INTERVIEW_LIST_FILTERS.map((value) => (
          <option key={value} value={value}>
            {t(`interviews.state.${value}`)}
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
    </div>
  );
}
