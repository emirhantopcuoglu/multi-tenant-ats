import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input, Modal } from '@/components/ui';
import type { RescheduleRequest } from '@/types/interview';

interface RescheduleModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Current values, used to pre-fill the form. */
  scheduledAtUtc: string;
  durationMinutes: number;
  submitting: boolean;
  onConfirm: (body: RescheduleRequest) => void;
}

const MIN_DURATION_MINUTES = 1;

/* Convert a UTC ISO instant to the local "YYYY-MM-DDTHH:mm" string a datetime-local input expects.
   Subtracting the timezone offset shifts the UTC clock to local wall-clock time before formatting. */
function toDateTimeLocalValue(iso: string): string {
  const date = new Date(iso);
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

/* Reschedule form: a new date+time and duration. Controlled-confirm like the reject dialog — the page
   owns the mutation and toast; this component only collects and validates input. */
export function RescheduleModal({
  open,
  onOpenChange,
  scheduledAtUtc,
  durationMinutes,
  submitting,
  onConfirm,
}: RescheduleModalProps) {
  const { t } = useTranslation();
  const [scheduledAt, setScheduledAt] = useState('');
  const [duration, setDuration] = useState('');
  const [errors, setErrors] = useState<{ scheduledAt?: string; duration?: string }>({});

  // Re-seed from the current interview every time the modal opens.
  useEffect(() => {
    if (open) {
      setScheduledAt(toDateTimeLocalValue(scheduledAtUtc));
      setDuration(String(durationMinutes));
      setErrors({});
    }
  }, [open, scheduledAtUtc, durationMinutes]);

  const handleConfirm = () => {
    const parsedDuration = Number(duration);
    const nextErrors: typeof errors = {};
    if (!scheduledAt) nextErrors.scheduledAt = t('interviews.form.whenRequired');
    if (!Number.isFinite(parsedDuration) || parsedDuration < MIN_DURATION_MINUTES)
      nextErrors.duration = t('interviews.form.durationInvalid');

    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      return;
    }

    onConfirm({
      scheduledAtUtc: new Date(scheduledAt).toISOString(),
      durationMinutes: parsedDuration,
    });
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
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label={t('interviews.form.when')} error={errors.scheduledAt}>
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              type="datetime-local"
              aria-describedby={describedById}
              invalid={invalid}
              value={scheduledAt}
              onChange={(event) => setScheduledAt(event.target.value)}
            />
          )}
        </Field>

        <Field label={t('interviews.form.duration')} error={errors.duration}>
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              type="number"
              min={MIN_DURATION_MINUTES}
              aria-describedby={describedById}
              invalid={invalid}
              value={duration}
              onChange={(event) => setDuration(event.target.value)}
            />
          )}
        </Field>
      </div>
    </Modal>
  );
}
