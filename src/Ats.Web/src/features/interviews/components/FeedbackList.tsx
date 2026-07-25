import { useTranslation } from 'react-i18next';
import { Badge, Skeleton } from '@/components/ui';
import { recommendationTone } from '@/lib/statusColors';
import { fullName, useUserLookup } from '@/features/users/useUsers';
import type { InterviewFeedbackSummary } from '@/types/interview';
import { StarRating } from './StarRating';

interface FeedbackListProps {
  summary: InterviewFeedbackSummary | undefined;
  isLoading: boolean;
}

/* Reads back what the panel actually said — the half of this feature that did not exist: evaluations
   were written and no query ever returned them, so a rating vanished into the database the moment it
   was submitted.

   Every state here comes from the server's answer rather than being re-derived: `isWithheld` is the
   backend's decision that this caller must file their own evaluation first, and reproducing that
   rule on the client would only create a second place for it to drift. */
export function FeedbackList({ summary, isLoading }: FeedbackListProps) {
  const { t, i18n } = useTranslation();
  const lookup = useUserLookup();

  if (isLoading) return <Skeleton className="h-20 w-full" />;
  if (!summary) return null;

  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' });

  const progress = (
    <p className="text-sm text-text-muted">
      {t('interviews.feedback.progress', {
        submitted: summary.submittedCount,
        expected: summary.expectedCount,
      })}
    </p>
  );

  if (summary.isWithheld) {
    return (
      <div className="space-y-2">
        {progress}
        <p className="rounded-xl bg-divider/60 px-3 py-2 text-sm text-text-muted">
          {t('interviews.feedback.withheld')}
        </p>
      </div>
    );
  }

  if (summary.items.length === 0) {
    return (
      <div className="space-y-2">
        {progress}
        <p className="text-sm text-text-muted">{t('interviews.feedback.none')}</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        {progress}
        {summary.averageRating !== null && (
          <p className="text-sm text-text">
            {t('interviews.feedback.average', { rating: summary.averageRating.toFixed(1) })}
          </p>
        )}
      </div>

      <ul className="space-y-3">
        {summary.items.map((item) => {
          const author = lookup.get(item.interviewerUserId);
          return (
            <li key={item.id} className="space-y-2 rounded-2xl bg-divider/60 px-4 py-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="text-sm font-medium text-text">
                  {author ? fullName(author) : t('interviews.feedback.unknownInterviewer')}
                </span>
                <span className="text-xs text-text-muted">
                  {dateFormatter.format(new Date(item.submittedAtUtc))}
                </span>
              </div>

              <div className="flex flex-wrap items-center gap-3">
                <StarRating value={item.rating} readOnly ariaLabel={t('interviews.feedback.rating')} />
                <Badge tone={recommendationTone[item.recommendation]}>
                  {t(`recommendation.${item.recommendation}`)}
                </Badge>
              </div>

              {item.comments && (
                <p className="whitespace-pre-wrap text-sm text-text-muted">{item.comments}</p>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
