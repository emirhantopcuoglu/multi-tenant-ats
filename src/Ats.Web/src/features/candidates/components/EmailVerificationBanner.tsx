import { useTranslation } from 'react-i18next';
import { Button, useToast } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { useResendCandidateVerification } from '../useCandidateEmailVerification';

/* Renders nothing once verified. A banner, not a block: unverified accounts can do everything but apply,
   including fixing a mistyped address. */
export function EmailVerificationBanner() {
  const { t } = useTranslation();
  const { toast } = useToast();
  const { user } = useAuth();
  const resend = useResendCandidateVerification();

  if (user?.kind !== 'candidate' || user.isEmailVerified) return null;

  const handleResend = () => {
    resend.mutate(undefined, {
      onSuccess: () =>
        toast({ title: t('candidatePortal.verifyEmail.resent', { email: user.email }), tone: 'success' }),
      onError: () => toast({ title: t('candidatePortal.verifyEmail.resendError'), tone: 'danger' }),
    });
  };

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-warning/40 bg-warning-bg px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
      <div className="space-y-0.5">
        <p className="text-sm font-medium text-text">{t('candidatePortal.verifyEmail.bannerTitle')}</p>
        <p className="text-sm text-text-muted">
          {t('candidatePortal.verifyEmail.bannerBody', { email: user.email })}
        </p>
      </div>
      <Button variant="secondary" onClick={handleResend} disabled={resend.isPending} className="shrink-0">
        {t('candidatePortal.verifyEmail.resend')}
      </Button>
    </div>
  );
}
