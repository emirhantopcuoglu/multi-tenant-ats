import { useTranslation } from 'react-i18next';
import { Button, useToast } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { useResendCandidateVerification } from '../useCandidateEmailVerification';

/* Shown to a signed-in candidate whose address is still unproven. Renders nothing otherwise, so the
   pages that include it need no condition of their own.

   Deliberately a banner rather than a blocking screen: an unverified account is fully usable except
   for applying. Locking the portal would strand anyone who mistyped their address, since the email is
   already taken and they cannot register again — they need to get in to correct it from settings. */
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
