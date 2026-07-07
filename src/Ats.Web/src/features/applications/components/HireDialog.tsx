import { useTranslation } from 'react-i18next';
import { Button, Modal } from '@/components/ui';

interface HireDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
  submitting: boolean;
}

/* Hire confirmation: the decision is terminal (the application can never be moved again) and the
   candidate is congratulated immediately, so a plain confirm gate prevents a misclick from
   sending someone a hire message by accident. */
export function HireDialog({ open, onOpenChange, onConfirm, submitting }: HireDialogProps) {
  const { t } = useTranslation();

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('applicationDetail.hire_modal.title')}
      description={t('applicationDetail.hire_modal.description')}
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" onClick={onConfirm} disabled={submitting}>
            {t('applicationDetail.hire_modal.confirm')}
          </Button>
        </>
      }
    />
  );
}
