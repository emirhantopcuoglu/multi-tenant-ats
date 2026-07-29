import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input, Modal } from '@/components/ui';
import {
  DEFAULT_INTERVIEW_DURATION,
  INTERVIEW_DURATION_OPTIONS,
} from '@/types/enums';
import type { RescheduleRequest } from '@/types/interview';
import { MINIMUM_LEAD_MINUTES, isSlotTooSoon, toScheduledAt } from '../scheduleValidation';

interface RescheduleModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Current values, used to pre-fill the form. */
  scheduledAtUtc: string;
  durationMinutes: number;
  submitting: boolean;
  onConfirm: (body: RescheduleRequest) => void;
}

/* Split a UTC ISO instant into the local YYYY-MM-DD and HH:mm a native date/time input expects.
   Shifting by the timezone offset turns the UTC clock into local wall-clock before formatting. */
function toLocalParts(iso: string): { date: string; time: string } {
  const local = new Date(new Date(iso).getTime() - new Date(iso).getTimezoneOffset() * 60_000);
  const [date, clock] = local.toISOString().split('T');
  return { date, time: clock.slice(0, 5) };
}

function localDateInputValue(date: Date): string {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}

const isPreset = (minutes: number): boolean =>
  (INTERVIEW_DURATION_OPTIONS as readonly number[]).includes(minutes);

/* Reschedule form: a new date + time and a preset duration. Controlled-confirm like the reject
   dialog — the page owns the mutation and toast; this component only collects and validates input. */
export function RescheduleModal({
  open,
  onOpenChange,
  scheduledAtUtc,
  durationMinutes,
  submitting,
  onConfirm,
}: RescheduleModalProps) {
  const { t } = useTranslation();
  const [date, setDate] = useState('');
  const [time, setTime] = useState('');
  const [duration, setDuration] = useState<number>(DEFAULT_INTERVIEW_DURATION);
  const [errors, setErrors] = useState<{ date?: string; time?: string }>({});

  // Re-seed from the current interview when the modal opens. A legacy off-preset duration falls
  // back to the default so the form can only ever submit an allowed value. Adjusted during render
  // rather than in an effect, which also narrows the trigger: the effect depended on the interview's
  // own fields, so a background refetch re-seeded the form mid-edit. Only the open transition does.
  const [prevOpen, setPrevOpen] = useState(open);
  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) {
      const parts = toLocalParts(scheduledAtUtc);
      setDate(parts.date);
      setTime(parts.time);
      setDuration(isPreset(durationMinutes) ? durationMinutes : DEFAULT_INTERVIEW_DURATION);
      setErrors({});
    }
  }

  const handleConfirm = () => {
    const nextErrors: typeof errors = {};
    if (!date) nextErrors.date = t('interviews.form.dateRequired');
    if (!time) nextErrors.time = t('interviews.form.timeRequired');

    const scheduledAt = toScheduledAt(date, time);
    if (scheduledAt && isSlotTooSoon(scheduledAt))
      nextErrors.time = t('interviews.form.whenTooSoon', { count: MINIMUM_LEAD_MINUTES });

    if (Object.keys(nextErrors).length > 0 || !scheduledAt) {
      setErrors(nextErrors);
      return;
    }

    onConfirm({ scheduledAtUtc: scheduledAt.toISOString(), durationMinutes: duration });
  };

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('interviews.reschedule.title')}
      description={t('interviews.reschedule.description')}
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button onClick={handleConfirm} disabled={submitting}>
            {t('interviews.reschedule.confirm')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field label={t('interviews.form.date')} error={errors.date}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="date"
                min={localDateInputValue(new Date())}
                aria-describedby={describedById}
                invalid={invalid}
                value={date}
                onChange={(event) => setDate(event.target.value)}
              />
            )}
          </Field>

          <Field label={t('interviews.form.time')} error={errors.time}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="time"
                aria-describedby={describedById}
                invalid={invalid}
                value={time}
                onChange={(event) => setTime(event.target.value)}
              />
            )}
          </Field>
        </div>

        <div className="space-y-1.5">
          <span className="block text-sm font-medium text-text">{t('interviews.form.duration')}</span>
          <div className="flex flex-wrap gap-2">
            {INTERVIEW_DURATION_OPTIONS.map((value) => (
              <button
                key={value}
                type="button"
                aria-pressed={duration === value}
                onClick={() => setDuration(value)}
                className={
                  'rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors ' +
                  (duration === value
                    ? 'border-transparent bg-accent text-accent-fg'
                    : 'border-border bg-card text-text hover:bg-divider')
                }
              >
                {t('interviews.minutesShort', { count: value })}
              </button>
            ))}
          </div>
        </div>
      </div>
    </Modal>
  );
}
