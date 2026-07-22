import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge, Card, EmptyState, Skeleton } from '@/components/ui';
import { interviewStatusTone } from '@/lib/statusColors';
import { PublicLayout } from '@/features/public/components/PublicLayout';
import { useCandidateInterviews } from '../useCandidateInterviews';

function CalendarIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="3" y="5" width="18" height="16" rx="2" />
      <path d="M16 3v4M8 3v4M3 10h18" />
    </svg>
  );
}

/* The candidate's own interviews across every application they hold, regardless of company — the
   counterpart to CandidateInterviewsCard, which only shows one application's interviews. Each row
   links into the (future) live room; the room page itself decides whether it's actually reachable
   yet, so this list never needs to know the current time. */
export function CandidateInterviewsPage() {
  const { t, i18n } = useTranslation();
  const query = useCandidateInterviews();
  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'long', timeStyle: 'short' });

  const interviews = query.data ?? [];

  return (
    <PublicLayout>
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">{t('candidatePortal.interviews.title')}</h1>
          <p className="text-sm text-text-muted">{t('candidatePortal.interviews.subtitle')}</p>
        </div>

        {query.isLoading ? (
          <div className="space-y-3" aria-busy="true">
            <Skeleton className="h-20 w-full" />
            <Skeleton className="h-20 w-full" />
          </div>
        ) : query.isError ? (
          <EmptyState
            title={t('candidatePortal.loadError')}
            action={
              <button
                type="button"
                onClick={() => void query.refetch()}
                className="text-sm font-medium text-accent hover:underline"
              >
                {t('candidatePortal.retry')}
              </button>
            }
          />
        ) : interviews.length === 0 ? (
          <EmptyState title={t('candidatePortal.interviews.empty')} />
        ) : (
          <ul className="space-y-3">
            {interviews.map((interview) => (
              <li key={interview.id}>
                <Card className="flex flex-wrap items-center justify-between gap-4 py-4">
                  <div className="flex items-center gap-3">
                    <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-accent-subtle text-accent">
                      <CalendarIcon />
                    </span>
                    <div className="space-y-0.5">
                      <p className="text-sm font-medium text-text">
                        {t(`interviewType.${interview.type}`)} · {interview.jobTitle}
                      </p>
                      <p className="text-xs text-text-muted">
                        {interview.companyName} · {dateFormatter.format(new Date(interview.scheduledAtUtc))}
                        {' · '}
                        {t('interviews.minutesShort', { count: interview.durationMinutes })}
                      </p>
                    </div>
                  </div>

                  <div className="flex shrink-0 items-center gap-3">
                    <Badge tone={interviewStatusTone[interview.status]} dot>
                      {t(`status.${interview.status}`)}
                    </Badge>
                    <Link
                      to={`/interview-room/${interview.roomToken}`}
                      className="text-sm font-medium text-accent hover:underline"
                    >
                      {t('candidatePortal.interviews.openRoom')}
                    </Link>
                  </div>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </div>
    </PublicLayout>
  );
}
