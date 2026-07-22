import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, useToast } from '@/components/ui';
import { toApiError } from '@/lib/problemDetails';
import { useDeleteCandidateAccount, useFreezeCandidateAccount } from '../useCandidateAccount';
import { FreezeAccountDialog } from '../components/FreezeAccountDialog';
import { DeleteAccountDialog } from '../components/DeleteAccountDialog';

const DELETE_INVALID_PASSWORD_CODE = 'candidate_account.invalid_current_password';

/* The Hesap tab of /candidate/settings: the danger zone. Freeze (reversible, one click undoes it
   from the reactivation screen) and delete (permanent — the backend anonymizes the row) each get
   their own card and their own confirmation dialog, so neither action fires on a stray click. */
export function CandidateAccountSettingsTab() {
  const { t } = useTranslation();
  const { toast } = useToast();
  const freeze = useFreezeCandidateAccount();
  const deleteAccount = useDeleteCandidateAccount();

  const [freezeDialogOpen, setFreezeDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deletePasswordError, setDeletePasswordError] = useState<string | undefined>();

  const handleFreeze = () => {
    freeze.mutate(undefined, {
      onSuccess: () => setFreezeDialogOpen(false),
      onError: () => toast({ title: t('candidateSettings.account.freezeError'), tone: 'danger' }),
    });
  };

  const handleDelete = (currentPassword: string) => {
    setDeletePasswordError(undefined);
    deleteAccount.mutate(
      { currentPassword },
      {
        onSuccess: () => {
          /* The success hook already ended the session and routed to the login page; the toast is
             the only trace of why. */
          toast({ title: t('candidateSettings.account.deleted'), tone: 'success' });
        },
        onError: (error) => {
          if (toApiError(error).code === DELETE_INVALID_PASSWORD_CODE) {
            setDeletePasswordError(t('candidateSettings.currentPasswordInvalid'));
          } else {
            toast({ title: t('candidateSettings.account.deleteError'), tone: 'danger' });
          }
        },
      },
    );
  };

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h2 className="text-lg font-semibold tracking-tight">{t('candidateSettings.account.heading')}</h2>
        <p className="text-sm text-text-muted">{t('candidateSettings.account.subheading')}</p>
      </div>

      <Card className="max-w-xl space-y-3">
        <div className="space-y-1">
          <h3 className="text-sm font-medium">{t('candidateSettings.account.freezeTitle')}</h3>
          <p className="text-sm text-text-muted">{t('candidateSettings.account.freezeDescription')}</p>
        </div>
        <div className="flex justify-end">
          <Button type="button" variant="secondary" onClick={() => setFreezeDialogOpen(true)}>
            {t('candidateSettings.account.freezeSubmit')}
          </Button>
        </div>
      </Card>

      <Card className="max-w-xl space-y-3">
        <div className="space-y-1">
          <h3 className="text-sm font-medium text-danger">{t('candidateSettings.account.deleteTitle')}</h3>
          <p className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {t('candidateSettings.account.deleteWarning')}
          </p>
        </div>
        <div className="flex justify-end">
          <Button type="button" variant="danger" onClick={() => setDeleteDialogOpen(true)}>
            {t('candidateSettings.account.deleteSubmit')}
          </Button>
        </div>
      </Card>

      <FreezeAccountDialog
        open={freezeDialogOpen}
        onOpenChange={setFreezeDialogOpen}
        onConfirm={handleFreeze}
        submitting={freeze.isPending}
      />
      <DeleteAccountDialog
        open={deleteDialogOpen}
        onOpenChange={setDeleteDialogOpen}
        onConfirm={handleDelete}
        submitting={deleteAccount.isPending}
        passwordError={deletePasswordError}
      />
    </div>
  );
}
