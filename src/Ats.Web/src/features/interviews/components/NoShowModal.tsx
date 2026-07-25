import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Field, Modal, Select } from '@/components/ui';
import { NO_SHOW_PARTIES, type MarkNoShowRequest, type NoShowParty } from '@/types/interview';

interface NoShowModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  submitting: boolean;
  onConfirm: (body: MarkNoShowRequest) => void;
}

/* Marking a no-show now asks which side failed to appear. It is a short dialog for a reason that is
   not cosmetic: "the candidate did not turn up" is a signal about that candidate, while "the
   interviewer did not" is our own failure and must never end up counting against the person who did
   show up. One button recording both would make the field useless for either purpose. */
export function NoShowModal({ open, onOpenChange, submitting, onConfirm }: NoShowModalProps) {
  const { t } = useTranslation();
  const [party, setParty] = useState<NoShowParty | ''>('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setParty('');
      setError(null);
    }
  }, [open]);

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
