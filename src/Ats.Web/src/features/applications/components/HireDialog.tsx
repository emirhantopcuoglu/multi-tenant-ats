import { useTranslation } from 'react-i18next';
import { Button, Modal } from '@/components/ui';

interface HireDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
  submitting: boolean;
}

/* Hiring is final and emails the candidate right away, so it needs a confirmation. */
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
