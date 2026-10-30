import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Field, Modal, Select } from '@/components/ui';
import { NO_SHOW_PARTIES, type MarkNoShowRequest, type NoShowParty } from '@/types/interview';

interface NoShowModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  submitting: boolean;
  onConfirm: (body: MarkNoShowRequest) => void;
}

/* Asks which side didn't show: an interviewer no-show must not count against the candidate. */
export function NoShowModal({ open, onOpenChange, submitting, onConfirm }: NoShowModalProps) {
  const { t } = useTranslation();
  const [party, setParty] = useState<NoShowParty | ''>('');
  const [error, setError] = useState<string | null>(null);

  // Reset when the dialog opens. Done during render, not in an effect, so the old draft is never painted.
  const [prevOpen, setPrevOpen] = useState(open);
  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) {
      setParty('');
      setError(null);
    }
  }

  const handleConfirm = () => {
    if (!party) {
      setError(t('interviews.noShow.partyRequired'));
      return;
    }

    onConfirm({ party });
  };

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('interviews.noShow.title')}
      description={t('interviews.noShow.description')}
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button onClick={handleConfirm} disabled={submitting}>
            {t('interviews.noShow.confirm')}
          </Button>
        </>
      }
    >
      <Field label={t('interviews.noShow.party')} error={error ?? undefined}>
        {({ id, describedById, invalid }) => (
          <Select
            id={id}
            aria-describedby={describedById}
            invalid={invalid}
            value={party}
            onChange={(event) => {
              setParty(event.target.value as NoShowParty);
              if (error) setError(null);
            }}
          >
            <option value="">{t('interviews.noShow.partyPlaceholder')}</option>
            {NO_SHOW_PARTIES.map((value) => (
              <option key={value} value={value}>
                {t(`noShowParty.${value}`)}
              </option>
            ))}
          </Select>
        )}
      </Field>
    </Modal>
  );
}
