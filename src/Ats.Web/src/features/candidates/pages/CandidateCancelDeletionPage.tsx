import { Navigate, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, useToast } from '@/components/ui';
import { AuthLayout } from '@/features/auth/components/AuthLayout';
import { useAuth } from '@/app/auth/auth-context';
import { useCancelCandidateAccountDeletion } from '../useCandidateAccount';

/* Signing in does not cancel the deletion by itself; the candidate has to choose it here. */
export function CandidateCancelDeletionPage() {
  const { t, i18n } = useTranslation();
  const { user, logout } = useAuth();
  const { toast } = useToast();
  const navigate = useNavigate();
  const cancelDeletion = useCancelCandidateAccountDeletion();

  if (user?.kind !== 'candidate' || user.status !== 'PendingDeletion' || !user.deletionDueAtUtc) {
    return <Navigate to="/candidate/settings" replace />;
  }

  const dueDate = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'long' }).format(
    new Date(user.deletionDueAtUtc),
  );

  return (
    <AuthLayout
      title={t('cancelAccountDeletion.title')}
      subtitle={t('cancelAccountDeletion.description', { date: dueDate })}
    >
      <div className="space-y-4">
        {cancelDeletion.isError && (
          <div role="alert" className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {t('cancelAccountDeletion.failed')}
          </div>
        )}

        <Button
          type="button"
          className="w-full"
          disabled={cancelDeletion.isPending}
          onClick={() =>
            cancelDeletion.mutate(undefined, {
              onSuccess: () => {
                toast({ title: t('cancelAccountDeletion.done'), tone: 'success' });
                navigate('/candidate/settings', { replace: true });
              },
            })
          }
        >
          {t('cancelAccountDeletion.confirm')}
        </Button>

        <Button type="button" variant="ghost" className="w-full" onClick={() => void logout()}>
          {t('cancelAccountDeletion.signOut')}
        </Button>
      </div>
    </AuthLayout>
  );
}
