import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input, Modal } from '@/components/ui';

/* Kept as a literal English word in every locale — like GitHub asking for the repo name, it is a
   deliberate action, not a message to translate. Comparison is case-sensitive on purpose: a typo
   should not read as consent. */
const DELETE_CONFIRMATION_WORD = 'DELETE';

interface DeleteAccountDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (currentPassword: string) => void;
  submitting: boolean;
  /** Server-side "wrong password" error, surfaced under the password field. */
  passwordError?: string;
}

/* Delete is permanent — the backend anonymizes the row in place. On top of the current password the
   backend already requires, typing the literal word DELETE is a second, unrelated proof of intent
   that a stray click or a saved autofill can't satisfy by accident. */
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

  // Reset when the dialog opens, so a previous draft never lingers. Adjusted during render rather
  // than in an effect: React discards the in-progress output and re-renders before committing, so
  // the cleared form is what reaches the DOM. An effect would paint the stale draft first and clear
  // it afterwards, which is the cascading render the linter flags.
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
