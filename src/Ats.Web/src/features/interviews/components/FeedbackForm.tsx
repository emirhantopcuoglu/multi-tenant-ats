import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Field, Select, Textarea, useToast } from '@/components/ui';
import { toApiError } from '@/lib/problemDetails';
import { FEEDBACK_RECOMMENDATIONS, type FeedbackRecommendation } from '@/types/enums';
import { useSubmitFeedback } from '../useInterviews';
import { StarRating } from './StarRating';

interface FeedbackFormProps {
  interviewId: string;
}

const MIN_RATING = 1;

/* Feedback form for an assigned interviewer. The parent only renders this once it has decided the user
   is eligible (assigned + not cancelled), but the backend is still the source of truth, so we map its
   error codes: a 409 "duplicate" flips us to the submitted state, while "not eligible"/403 surface as
   an inline message. */
export function FeedbackForm({ interviewId }: FeedbackFormProps) {
  const { t } = useTranslation();
  const { toast } = useToast();
  const submit = useSubmitFeedback(interviewId);

  const [rating, setRating] = useState(0);
  const [recommendation, setRecommendation] = useState<FeedbackRecommendation | ''>('');
  const [comments, setComments] = useState('');
  const [errors, setErrors] = useState<{ rating?: string; recommendation?: string }>({});
  const [submitted, setSubmitted] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);

  if (submitted) {
    return <p className="text-sm text-success">{t('interviews.feedback.submitted')}</p>;
  }

  const handleSubmit = () => {
    const nextErrors: typeof errors = {};
    if (rating < MIN_RATING) nextErrors.rating = t('interviews.feedback.ratingRequired');
    if (!recommendation) nextErrors.recommendation = t('interviews.feedback.recommendationRequired');
    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      return;
    }

    setServerError(null);
    submit.mutate(
      { rating, recommendation: recommendation as FeedbackRecommendation, comments: comments.trim() || undefined },
      {
        onSuccess: () => {
          setSubmitted(true);
          toast({ title: t('interviews.feedback.toast'), tone: 'success' });
        },
        onError: (error) => {
          const { code } = toApiError(error);
          // A duplicate means feedback already exists for this user — treat it as done, not a failure.
          if (code === 'interview.duplicate_feedback') {
            setSubmitted(true);
            return;
          }
          setServerError(
            code === 'interview.feedback_not_eligible' || code === 'http_403'
              ? t('interviews.feedback.notEligible')
              : t('interviews.toast.error'),
          );
        },
      },
    );
  };

  return (
    <div className="space-y-4">
      <Field label={t('interviews.feedback.rating')} error={errors.rating}>
        {({ describedById }) => (
          <StarRating
            value={rating}
            onChange={(next) => {
              setRating(next);
              if (errors.rating) setErrors((current) => ({ ...current, rating: undefined }));
            }}
            ariaLabel={t('interviews.feedback.rating')}
            describedById={describedById}
          />
        )}
      </Field>

      <Field label={t('interviews.feedback.recommendation')} error={errors.recommendation}>
        {({ id, describedById, invalid }) => (
          <Select
            id={id}
            aria-describedby={describedById}
            invalid={invalid}
            value={recommendation}
            onChange={(event) => {
              setRecommendation(event.target.value as FeedbackRecommendation);
              if (errors.recommendation) setErrors((current) => ({ ...current, recommendation: undefined }));
            }}
          >
            <option value="">{t('interviews.feedback.recommendationPlaceholder')}</option>
            {FEEDBACK_RECOMMENDATIONS.map((value) => (
              <option key={value} value={value}>
                {t(`recommendation.${value}`)}
              </option>
            ))}
          </Select>
        )}
      </Field>

      <Field label={t('interviews.feedback.comments')}>
        {({ id }) => (
          <Textarea
            id={id}
            rows={3}
            value={comments}
            onChange={(event) => setComments(event.target.value)}
            placeholder={t('interviews.feedback.commentsPlaceholder')}
          />
        )}
      </Field>

      {serverError && <p className="text-sm text-danger">{serverError}</p>}

      <Button onClick={handleSubmit} disabled={submit.isPending}>
        {t('interviews.feedback.submit')}
      </Button>
    </div>
  );
}
