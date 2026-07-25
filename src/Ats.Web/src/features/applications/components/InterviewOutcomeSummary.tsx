import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui';
import { recommendationTone } from '@/lib/statusColors';
import { FEEDBACK_RECOMMENDATIONS } from '@/types/enums';
import type { ApplicationInterviewOutcome } from '@/types/interview';

interface InterviewOutcomeSummaryProps {
  outcome: ApplicationInterviewOutcome | undefined;
}

/* What the interviews concluded, on the application itself. Feedback existed only per interview
   before this, so deciding what to do next meant opening each one and remembering the scores.

   Renders nothing when there are no interviews — an empty summary on every pre-interview
   application would be noise. */
export function InterviewOutcomeSummary({ outcome }: InterviewOutcomeSummaryProps) {
  const { t } = useTranslation();

  if (!outcome || outcome.totalCount === 0) return null;

  const isFeedbackComplete =
    outcome.expectedFeedbackCount > 0 && outcome.feedbackCount >= outcome.expectedFeedbackCount;

  return (
    <div className="space-y-3 rounded-2xl bg-divider/60 px-4 py-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-sm font-medium text-text">{t('applicationDetail.outcome.title')}</span>
        {outcome.averageRating !== null && (
          <span className="text-sm text-text">
            {t('interviews.feedback.average', { rating: outcome.averageRating.toFixed(1) })}
          </span>
        )}
      </div>

      {/* The one thing that should stop a decision: a slot that passed with nothing recorded. */}
      {outcome.awaitingOutcomeCount > 0 && (
        <p className="text-sm text-warning">
          {t('applicationDetail.outcome.awaiting', { count: outcome.awaitingOutcomeCount })}
        </p>
      )}

      <p className="text-sm text-text-muted">
        {t('interviews.feedback.progress', {
          submitted: outcome.feedbackCount,
          expected: outcome.expectedFeedbackCount,
        })}
      </p>

      {/* Ordered by the enum, most negative first, so a split panel reads consistently. */}
      {outcome.feedbackCount > 0 && (
        <div className="flex flex-wrap gap-2">
          {FEEDBACK_RECOMMENDATIONS.map((recommendation) => {
            const count = outcome.recommendationCounts[recommendation] ?? 0;
            if (count === 0) return null;
            return (
              <Badge key={recommendation} tone={recommendationTone[recommendation]}>
                {t(`recommendation.${recommendation}`)} · {count}
              </Badge>
            );
          })}
        </div>
      )}

      {isFeedbackComplete && outcome.awaitingOutcomeCount === 0 && (
        <p className="text-sm text-text">{t('applicationDetail.outcome.readyForDecision')}</p>
      )}
    </div>
  );
}
