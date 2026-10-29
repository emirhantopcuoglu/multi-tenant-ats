import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Field, Modal, Select, Textarea } from '@/components/ui';
import {
  INTERVIEW_CANCELLATION_REASONS,
  type CancelInterviewRequest,
  type SelectableInterviewCancellationReason,
} from '@/types/interview';

interface CancelInterviewModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  submitting: boolean;
  onConfirm: (body: CancelInterviewRequest) => void;
}

const MAX_NOTE_LENGTH = 500;

/* The reason decides what the candidate's email says. The note is labelled internal: it never
   leaves the company side. */
export function CancelInterviewModal({
  open,
  onOpenChange,
  submitting,
  onConfirm,
}: CancelInterviewModalProps) {
  const { t } = useTranslation();
  const [reason, setReason] = useState<SelectableInterviewCancellationReason | ''>('');
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);

  // Reset when the dialog opens. Done during render, not in an effect, so the old draft is never painted.
  const [prevOpen, setPrevOpen] = useState(open);
  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) {
      setReason('');
      setNote('');
      setError(null);
    }
  }

  const handleConfirm = () => {
    if (!reason) {
      setError(t('interviews.cancel.reasonRequired'));
      return;
    }

    onConfirm({ reason, note: note.trim() || undefined });
  };

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('interviews.cancel.title')}
      description={t('interviews.cancel.description')}
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="danger" onClick={handleConfirm} disabled={submitting}>
            {t('interviews.cancel.confirm')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label={t('interviews.cancel.reason')} error={error ?? undefined}>
          {({ id, describedById, invalid }) => (
            <Select
              id={id}
              aria-describedby={describedById}
              invalid={invalid}
              value={reason}
              onChange={(event) => {
                setReason(event.target.value as SelectableInterviewCancellationReason);
                if (error) setError(null);
              }}
            >
              <option value="">{t('interviews.cancel.reasonPlaceholder')}</option>
              {INTERVIEW_CANCELLATION_REASONS.map((value) => (
                <option key={value} value={value}>
                  {t(`cancellationReason.${value}`)}
                </option>
              ))}
            </Select>
          )}
        </Field>

        {/* Shows what the candidate will be told. */}
        {reason && (
          <p className="rounded-xl bg-divider/60 px-3 py-2 text-sm text-text-muted">
            {t(`interviews.cancel.effect.${reason}`)}
          </p>
        )}

        <Field label={t('interviews.cancel.note')}>
          {({ id }) => (
            <>
              <Textarea
                id={id}
                rows={3}
                maxLength={MAX_NOTE_LENGTH}
                value={note}
                onChange={(event) => setNote(event.target.value)}
                placeholder={t('interviews.cancel.notePlaceholder')}
              />
              <p className="pt-1 text-xs text-text-muted">{t('interviews.cancel.noteHint')}</p>
            </>
          )}
        </Field>
      </div>
    </Modal>
  );
}
