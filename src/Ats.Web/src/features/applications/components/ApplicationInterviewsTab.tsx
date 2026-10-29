import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Skeleton } from '@/components/ui';
import { useApplicationInterviewOutcome, useInterviews } from '@/features/interviews/useInterviews';
import { InterviewerAvatars } from '@/features/interviews/components/InterviewerAvatars';
import { InterviewStatusBadge } from '@/features/interviews/components/InterviewStatusBadge';
import { InterviewOutcomeSummary } from './InterviewOutcomeSummary';

const INTERVIEWS_PAGE_SIZE = 20;

function CalendarIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="3" y="5" width="18" height="16" rx="2" />
      <path d="M16 3v4M8 3v4M3 10h18" />
    </svg>
  );
}

interface ApplicationInterviewsTabProps {
  applicationId: string;
  canSchedule: boolean;
  /* The dialog is owned by the parent. */
  onSchedule: () => void;
}

/* Uses GET /interviews?applicationId=. The button here is how follow-up interviews are scheduled;
   the first one comes from moving the application into the Interview stage. */
export function ApplicationInterviewsTab({ applicationId, canSchedule, onSchedule }: ApplicationInterviewsTabProps) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' });
  const query = useInterviews({ page: 1, pageSize: INTERVIEWS_PAGE_SIZE, applicationId });
  const outcomeQuery = useApplicationInterviewOutcome(applicationId);

  const interviews = query.data?.items ?? [];

  const scheduleButton = canSchedule ? (
    <div className="flex justify-end">
      <Button variant="secondary" onClick={onSchedule}>
        {interviews.length > 0
          ? t('applicationDetail.scheduleAnotherInterview')
          : t('interviews.schedule')}
      </Button>
    </div>
  ) : null;

  if (query.isLoading) {
    return (
      <div className="space-y-3" aria-busy="true">
        <Skeleton className="h-16 w-full" />
        <Skeleton className="h-16 w-full" />
      </div>
    );
  }

  if (query.isError) {
    return <p className="text-sm text-text-muted">{t('interviews.loadError')}</p>;
  }

  return (
    <div className="space-y-4">
      {scheduleButton}

      <InterviewOutcomeSummary outcome={outcomeQuery.data} />

      {interviews.length === 0 ? (
        <p className="text-sm text-text-muted">{t('interviews.empty.title')}</p>
      ) : (
        <ul className="space-y-3">
          {interviews.map((interview) => (
            <li
              key={interview.id}
              onClick={() => navigate(`/interviews/${interview.id}`)}
              className="flex cursor-pointer flex-wrap items-center justify-between gap-3 rounded-2xl bg-divider/60 px-4 py-3 transition-colors hover:bg-divider"
            >
              <div className="flex items-center gap-3">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-accent-subtle text-accent">
                  <CalendarIcon />
                </span>
                <div className="space-y-0.5">
                  <p className="text-sm font-medium text-text">{t(`interviewType.${interview.type}`)}</p>
                  <p className="text-xs text-text-muted">
                    {dateFormatter.format(new Date(interview.scheduledAtUtc))}
                    {' · '}
                    {t('interviews.minutesShort', { count: interview.durationMinutes })}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-3">
                <InterviewerAvatars interviewerUserIds={interview.interviewerUserIds} />
                <InterviewStatusBadge interview={interview} />
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
