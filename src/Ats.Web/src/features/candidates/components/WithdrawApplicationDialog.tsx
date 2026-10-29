import { useTranslation } from 'react-i18next';
import { Button, Modal } from '@/components/ui';

interface WithdrawApplicationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
  submitting: boolean;
  jobTitle: string;
  companyName: string;
}

/* Names the job and says that re-applying stays allowed. A plain click is enough: nothing is destroyed. */
export function WithdrawApplicationDialog({
  open,
  onOpenChange,
  onConfirm,
  submitting,
  jobTitle,
  companyName,
}: WithdrawApplicationDialogProps) {
  const { t } = useTranslation();

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('candidatePortal.withdraw.dialogTitle')}
      description={t('candidatePortal.withdraw.dialogDescription', { job: jobTitle, company: companyName })}
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="danger" onClick={onConfirm} disabled={submitting}>
            {t('candidatePortal.withdraw.dialogConfirm')}
          </Button>
        </>
      }
    >
      <p className="text-sm text-text-muted">{t('candidatePortal.withdraw.dialogConsequence')}</p>
    </Modal>
  );
}
