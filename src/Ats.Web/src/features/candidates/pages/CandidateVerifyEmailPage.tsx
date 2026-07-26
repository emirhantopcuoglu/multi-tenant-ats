import { useEffect, useRef } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card, EmptyState, Skeleton } from '@/components/ui';
import { PublicLayout } from '@/features/public/components/PublicLayout';
import { useVerifyCandidateEmail } from '../useCandidateEmailVerification';

/* Landing page for the mailed verification link. It fires the request on mount rather than showing a
   button: the click on the link in the email IS the candidate's confirmation, and asking them to
   confirm a second time here would be asking the same question twice.

   No auth guard — the link is opened from an email client, which carries no session, and possibly on
   a different device than the one that registered. The token is the credential. */
export function CandidateVerifyEmailPage() {
  const { t } = useTranslation();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const verify = useVerifyCandidateEmail();

  /* React 18 StrictMode mounts effects twice in development. The token is single-use, so a second
     call would consume it and then report "invalid link" for a verification that had just succeeded.
     A ref, not a state flag: it must survive a re-render without triggering one. */
  const attempted = useRef(false);
  useEffect(() => {
    if (attempted.current || token.length === 0) return;
    attempted.current = true;
    verify.mutate(token);
    // Fires once per mounted page; verify.mutate is stable and deliberately not a dependency.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  const backToApplications = (
    <Link to="/candidate/applications" className="text-sm font-medium text-accent hover:underline">
      {t('candidatePortal.verifyEmail.goToApplications')}
    </Link>
  );

  return (
    <PublicLayout>
      <Card className="py-12">
        {token.length === 0 || verify.isError ? (
          <EmptyState
            title={t('candidatePortal.verifyEmail.failedTitle')}
            description={t('candidatePortal.verifyEmail.failedBody')}
            action={backToApplications}
          />
        ) : verify.isSuccess ? (
          <EmptyState
            title={t('candidatePortal.verifyEmail.successTitle')}
            description={t('candidatePortal.verifyEmail.successBody')}
            action={backToApplications}
          />
        ) : (
          <div className="space-y-3" aria-busy="true">
            <Skeleton className="h-6 w-48" />
            <Skeleton className="h-4 w-full max-w-md" />
          </div>
        )}
      </Card>
    </PublicLayout>
  );
}
