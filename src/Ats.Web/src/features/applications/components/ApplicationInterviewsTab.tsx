import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge, Skeleton } from '@/components/ui';
import { interviewStatusTone } from '@/lib/statusColors';
import { useInterviews } from '@/features/interviews/useInterviews';
import { InterviewerAvatars } from '@/features/interviews/components/InterviewerAvatars';

const INTERVIEWS_PAGE_SIZE = 20;

function CalendarIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="3" y="5" width="18" height="16" rx="2" />
      <path d="M16 3v4M8 3v4M3 10h18" />
    </svg>
  );
}

/* The application detail's Interviews tab: every interview scheduled against this application,
   most recent first. Reuses the existing GET /interviews?applicationId= endpoint (useInterviews)
   rather than adding a new one — the same data the standalone /interviews page and the pipeline's
   auto-advance-on-schedule consumer both key off. Soft card rows instead of the dense
   InterviewsTable: a single application has few interviews, and the candidate column that table
   carries is redundant here. */
export function ApplicationInterviewsTab({ applicationId }: { applicationId: string }) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' });
  const query = useInterviews({ page: 1, pageSize: INTERVIEWS_PAGE_SIZE, applicationId });

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

  const interviews = query.data?.items ?? [];
  if (interviews.length === 0) {
    return <p className="text-sm text-text-muted">{t('interviews.empty.title')}</p>;
  }

  return (
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
            <Badge tone={interviewStatusTone[interview.status]} dot>
              {t(`status.${interview.status}`)}
            </Badge>
          </div>
        </li>
      ))}
    </ul>
  );
}
