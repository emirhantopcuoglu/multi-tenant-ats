import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Modal, Textarea } from '@/components/ui';

interface RejectDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (reason: string) => void;
  submitting: boolean;
}

/* Reject confirmation: a required free-text reason. The reason is sent to POST /{id}/reject and kept
   internal (the candidate's notification never includes it). */
export function RejectDialog({ open, onOpenChange, onConfirm, submitting }: RejectDialogProps) {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const [error, setError] = useState(false);

  // Reset the field each time the dialog opens, so a previous draft never lingers.
  useEffect(() => {
    if (open) {
      setReason('');
      setError(false);
    }
  }, [open]);

  const handleConfirm = () => {
    if (!reason.trim()) {
      setError(true);
      return;
    }
    onConfirm(reason.trim());
  };

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('applicationDetail.reject_modal.title')}
      description={t('applicationDetail.reject_modal.description')}
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="danger" onClick={handleConfirm} disabled={submitting}>
            {t('applicationDetail.reject_modal.confirm')}
          </Button>
        </>
      }
    >
      <div className="space-y-1.5">
        <label htmlFor="reject-reason" className="block text-sm font-medium text-text">
          {t('applicationDetail.reject_modal.reasonLabel')}
        </label>
        <Textarea
          id="reject-reason"
          value={reason}
          onChange={(event) => {
            setReason(event.target.value);
            if (error) setError(false);
          }}
          invalid={error}
          placeholder={t('applicationDetail.reject_modal.reasonPlaceholder')}
          rows={4}
        />
        {error && <p className="text-xs text-danger">{t('applicationDetail.reject_modal.reasonRequired')}</p>}
      </div>
    </Modal>
  );
}
