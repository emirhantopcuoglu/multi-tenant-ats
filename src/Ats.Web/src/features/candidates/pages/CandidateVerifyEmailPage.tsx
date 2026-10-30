import { useEffect, useRef } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card, EmptyState, Skeleton } from '@/components/ui';
import { PublicLayout } from '@/features/public/components/PublicLayout';
import { useVerifyCandidateEmail } from '../useCandidateEmailVerification';

/* Verifies on mount: clicking the link is the confirmation. No auth guard; it may be opened on
   another device. */
export function CandidateVerifyEmailPage() {
  const { t } = useTranslation();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const verify = useVerifyCandidateEmail();

  /* StrictMode runs effects twice in dev, and the token is single-use, so guard with a ref. */
  const attempted = useRef(false);
  useEffect(() => {
    if (attempted.current || token.length === 0) return;
    attempted.current = true;
    verify.mutate(token);
    // Runs once per mount; verify.mutate is stable.
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
