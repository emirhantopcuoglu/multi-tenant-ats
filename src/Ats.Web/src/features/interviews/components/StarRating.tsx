import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/cn';

interface StarRatingProps {
  value: number;
  /* Omitted with readOnly. */
  onChange?: (value: number) => void;
  ariaLabel: string;
  describedById?: string;
  max?: number;
  readOnly?: boolean;
}

function StarIcon({ filled }: { filled: boolean }) {
  return (
    <svg
      width="24"
      height="24"
      viewBox="0 0 24 24"
      fill={filled ? 'currentColor' : 'none'}
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
    </svg>
  );
}

/* Buttons with their own aria-label ("3 stars"); a radiogroup's arrow keys aren't worth it for five. */
export function StarRating({
  value,
  onChange,
  ariaLabel,
  describedById,
  max = 5,
  readOnly = false,
}: StarRatingProps) {
  const { t } = useTranslation();
  const stars = Array.from({ length: max }, (_, index) => index + 1);

  // Read-only: one labelled img instead of five disabled buttons.
  if (readOnly) {
    return (
      <div
        role="img"
        aria-label={t('interviews.feedback.ratingOf', { rating: value, max })}
        className="flex gap-1"
      >
        {stars.map((star) => (
          <span key={star} className={star <= value ? 'text-warning' : 'text-text-muted'}>
            <StarIcon filled={star <= value} />
          </span>
        ))}
      </div>
    );
  }

  return (
    <div role="group" aria-label={ariaLabel} aria-describedby={describedById} className="flex gap-1">
      {stars.map((star) => (
        <button
          key={star}
          type="button"
          onClick={() => onChange?.(star)}
          aria-label={t('interviews.feedback.stars', { count: star })}
          aria-pressed={star === value}
          className={cn(
            'rounded transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent',
            star <= value ? 'text-warning' : 'text-text-muted hover:text-warning',
          )}
        >
          <StarIcon filled={star <= value} />
        </button>
      ))}
    </div>
  );
}
