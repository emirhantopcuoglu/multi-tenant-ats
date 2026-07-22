import { useTranslation } from 'react-i18next';
import { Button, Modal } from '@/components/ui';

interface FreezeAccountDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
  submitting: boolean;
}

/* Freeze is reversible from the reactivation screen (Freeze() doesn't rotate the security stamp),
   so the dialog only asks for a plain click — unlike delete, no typed phrase is warranted. */
export function FreezeAccountDialog({
  open,
  onOpenChange,
  onConfirm,
  submitting,
}: FreezeAccountDialogProps) {
  const { t } = useTranslation();

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('candidateSettings.account.freezeDialogTitle')}
      description={t('candidateSettings.account.freezeDialogDescription')}
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button onClick={onConfirm} disabled={submitting}>
            {t('candidateSettings.account.freezeDialogConfirm')}
          </Button>
        </>
      }
    />
  );
}
