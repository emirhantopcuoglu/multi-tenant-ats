import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input, Modal } from '@/components/ui';

/* Not translated, and compared case-sensitively. */
const DELETE_CONFIRMATION_WORD = 'DELETE';

interface DeleteAccountDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (currentPassword: string) => void;
  submitting: boolean;
  passwordError?: string;
}

/* Deletion is permanent, so on top of the password the user types DELETE. */
export function DeleteAccountDialog({
  open,
  onOpenChange,
  onConfirm,
  submitting,
  passwordError,
}: DeleteAccountDialogProps) {
  const { t } = useTranslation();
  const [confirmWord, setConfirmWord] = useState('');
  const [password, setPassword] = useState('');
  const [wordTouched, setWordTouched] = useState(false);

  // Reset when the dialog opens. Done during render, not in an effect, so the old draft is never painted.
  const [prevOpen, setPrevOpen] = useState(open);
  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) {
      setConfirmWord('');
      setPassword('');
      setWordTouched(false);
    }
  }

  const isWordCorrect = confirmWord === DELETE_CONFIRMATION_WORD;

  const handleConfirm = () => {
    if (!isWordCorrect) {
      setWordTouched(true);
      return;
    }
    onConfirm(password);
  };

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('candidateSettings.account.deleteDialogTitle')}
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="danger" onClick={handleConfirm} disabled={submitting}>
            {t('candidateSettings.account.deleteDialogConfirm')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
          {t('candidateSettings.account.deleteWarning')}
        </p>

        <Field
          label={t('candidateSettings.account.deleteConfirmLabel', { word: DELETE_CONFIRMATION_WORD })}
          error={
            wordTouched && !isWordCorrect
              ? t('candidateSettings.account.deleteConfirmMismatch')
              : undefined
          }
        >
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              aria-describedby={describedById}
              invalid={invalid}
              autoComplete="off"
              value={confirmWord}
              onChange={(event) => setConfirmWord(event.target.value)}
            />
          )}
        </Field>

        <Field label={t('candidateSettings.currentPassword')} error={passwordError}>
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              type="password"
              autoComplete="current-password"
              aria-describedby={describedById}
              invalid={invalid}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          )}
        </Field>
      </div>
    </Modal>
  );
}
