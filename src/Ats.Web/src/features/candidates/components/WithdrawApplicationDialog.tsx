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

/* Withdrawal is irreversible — Application.Withdraw() is a terminal transition with no way back — so
   the dialog names the job and company rather than asking "are you sure?" about an unnamed thing. It
   also says the one consequence that is not obvious: the application closes for good, but re-applying
   later stays allowed (the backend's duplicate rule only blocks a second *active* application).

   No typed-phrase confirmation, unlike account deletion: nothing is destroyed and the candidate can
   apply again, so a plain click is proportionate. */
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
