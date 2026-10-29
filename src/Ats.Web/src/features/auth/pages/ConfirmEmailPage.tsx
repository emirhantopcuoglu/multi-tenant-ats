import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMutation } from '@tanstack/react-query';
import { Button, Card, EmptyState, Skeleton } from '@/components/ui';
import { AuthLayout } from '../components/AuthLayout';
import { confirmEmail, resendEmailConfirmation } from '../authApi';

/* Confirms on mount: clicking the link is the confirmation. No auth guard; nobody can sign in yet. */
export function ConfirmEmailPage() {
  const { t } = useTranslation();
  const [searchParams] = useSearchParams();
  const userId = searchParams.get('userId') ?? '';
  const token = searchParams.get('token') ?? '';

  const [resendEmail, setResendEmail] = useState('');
  const confirm = useMutation({ mutationFn: () => confirmEmail(userId, token) });
  const resend = useMutation({ mutationFn: (email: string) => resendEmailConfirmation(email) });

  /* StrictMode runs effects twice in dev; the ref keeps it to one request. */
  const attempted = useRef(false);
  useEffect(() => {
    if (attempted.current || userId.length === 0 || token.length === 0) return;
    attempted.current = true;
    confirm.mutate();
    // Runs once per mount; confirm.mutate is stable.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId, token]);

  const hasLink = userId.length > 0 && token.length > 0;

  if (confirm.isSuccess) {
    return (
      <AuthLayout title={t('auth.confirmEmail.successTitle')} subtitle={t('auth.confirmEmail.successBody')}>
        <Link to="/login" className="text-sm font-medium text-accent hover:underline">
          {t('auth.confirmEmail.goToLogin')}
        </Link>
      </AuthLayout>
    );
  }

  if (!hasLink || confirm.isError) {
    return (
      <AuthLayout title={t('auth.confirmEmail.failedTitle')} subtitle={t('auth.confirmEmail.failedBody')}>
        {/* Resend lives here too, since someone with an expired link lands on this page. */}
        <form
          className="space-y-3"
          onSubmit={(event) => {
            event.preventDefault();
            resend.mutate(resendEmail.trim());
          }}
        >
          <label htmlFor="resend-email" className="block text-sm font-medium text-text">
            {t('auth.confirmEmail.resendLabel')}
          </label>
          <input
            id="resend-email"
            type="email"
            required
            value={resendEmail}
            onChange={(event) => setResendEmail(event.target.value)}
            className="w-full rounded-lg border border-border bg-card px-3 py-2 text-sm text-text"
          />
          <Button type="submit" disabled={resend.isPending} className="w-full">
            {t('auth.confirmEmail.resendAction')}
          </Button>
          {/* Reported as success for any address, so the wording says "if the account exists". */}
          {resend.isSuccess && (
            <p className="text-sm text-success">{t('auth.confirmEmail.resendSent')}</p>
          )}
          {resend.isError && <p className="text-sm text-danger">{t('auth.confirmEmail.resendError')}</p>}
        </form>
        <Link to="/login" className="mt-4 block text-sm text-text-muted hover:text-accent">
          {t('auth.confirmEmail.goToLogin')}
        </Link>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title={t('auth.confirmEmail.pendingTitle')} subtitle="">
      <Card>
        <div className="space-y-3" aria-busy="true">
          <Skeleton className="h-4 w-full max-w-xs" />
          <Skeleton className="h-4 w-full max-w-md" />
        </div>
      </Card>
    </AuthLayout>
  );
}

/* Shown right after registering, before the link is clicked. */
export function RegistrationPendingPage({ email }: { email: string }) {
  const { t } = useTranslation();
  const resend = useMutation({ mutationFn: () => resendEmailConfirmation(email) });

  return (
    <EmptyState
      title={t('auth.confirmEmail.pendingTitle')}
      description={t('auth.confirmEmail.pendingBody', { email })}
      action={
        <div className="space-y-2">
          <Button variant="secondary" onClick={() => resend.mutate()} disabled={resend.isPending}>
            {t('auth.confirmEmail.resendAction')}
          </Button>
          {resend.isSuccess && (
            <p className="text-sm text-success">{t('auth.confirmEmail.resendSent')}</p>
          )}
        </div>
      }
    />
  );
}
