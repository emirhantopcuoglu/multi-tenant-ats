import { useTranslation } from 'react-i18next';
import { Badge, Card } from '@/components/ui';
import { interviewStatusTone } from '@/lib/statusColors';
import type { CandidateInterview } from '../candidateApplicationsApi';

function CalendarIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="3" y="5" width="18" height="16" rx="2" />
      <path d="M16 3v4M8 3v4M3 10h18" />
    </svg>
  );
}

/* Every interview scheduled against the application, most recent first — the candidate's own
   transparent view of what's ahead (or already happened). Shown above the tracking timeline so it
   is the first thing a candidate with an upcoming interview sees. */
export function CandidateInterviewsCard({ interviews }: { interviews: CandidateInterview[] }) {
  const { t, i18n } = useTranslation();
  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'long', timeStyle: 'short' });

  if (interviews.length === 0) return null;

  return (
    <Card className="space-y-4">
      <h2 className="text-lg font-semibold tracking-tight">
        {t('candidatePortal.tracking.interviews.title')}
      </h2>
      <ul className="space-y-3">
        {interviews.map((interview) => (
          <li
            key={interview.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-accent-subtle/40 px-4 py-3"
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
                  {interview.location ? ` · ${interview.location}` : ''}
                </p>
              </div>
            </div>
            <Badge tone={interviewStatusTone[interview.status]} dot>
              {t(`status.${interview.status}`)}
            </Badge>
          </li>
        ))}
      </ul>
    </Card>
  );
}
