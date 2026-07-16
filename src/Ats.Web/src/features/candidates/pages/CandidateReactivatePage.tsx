import { Navigate, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, useToast } from '@/components/ui';
import { AuthLayout } from '@/features/auth/components/AuthLayout';
import { useAuth } from '@/app/auth/auth-context';
import { useReactivateCandidateAccount } from '../useCandidateAccount';

/* Where RequireActiveCandidate sends a frozen account. The session itself is valid (freezing does
   not rotate the security stamp), so undoing the freeze is one authenticated call — no email loop,
   no support ticket. */
export function CandidateReactivatePage() {
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const { toast } = useToast();
  const navigate = useNavigate();
  const reactivate = useReactivateCandidateAccount();

  /* An active account has nothing to reactivate — e.g. a stale bookmark of this page. */
  if (user?.kind === 'candidate' && user.status === 'Active') {
    return <Navigate to="/candidate/profile" replace />;
  }

  return (
    <AuthLayout title={t('reactivateAccount.title')} subtitle={t('reactivateAccount.description')}>
      <div className="space-y-4">
        {reactivate.isError && (
          <div role="alert" className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {t('reactivateAccount.failed')}
          </div>
        )}

        <Button
          type="button"
          className="w-full"
          disabled={reactivate.isPending}
          onClick={() =>
            reactivate.mutate(undefined, {
              onSuccess: () => {
                toast({ title: t('reactivateAccount.done'), tone: 'success' });
                navigate('/candidate/profile', { replace: true });
              },
            })
          }
        >
          {t('reactivateAccount.confirm')}
        </Button>

        <Button type="button" variant="ghost" className="w-full" onClick={() => void logout()}>
          {t('reactivateAccount.signOut')}
        </Button>
      </div>
    </AuthLayout>
  );
}
