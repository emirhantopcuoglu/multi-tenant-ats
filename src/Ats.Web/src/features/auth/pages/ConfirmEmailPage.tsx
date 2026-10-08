import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMutation } from '@tanstack/react-query';
import { Button, Card, EmptyState, Skeleton } from '@/components/ui';
import { AuthLayout } from '../components/AuthLayout';
import { confirmEmail, resendEmailConfirmation } from '../authApi';

/* Landing page for the mailed confirmation link. Fires on mount rather than showing a button: the
   click on the link in the email IS the confirmation, and asking again here would be asking twice.

   No auth guard — nobody can sign in until this succeeds, which is the whole point. */
export function ConfirmEmailPage() {
  const { t } = useTranslation();
  const [searchParams] = useSearchParams();
  const userId = searchParams.get('userId') ?? '';
  const token = searchParams.get('token') ?? '';

  const [resendEmail, setResendEmail] = useState('');
  const confirm = useMutation({ mutationFn: () => confirmEmail(userId, token) });
  const resend = useMutation({ mutationFn: (email: string) => resendEmailConfirmation(email) });

  /* StrictMode mounts effects twice in development. Identity's token is not single-use the way the
     candidate side's row is, so a double call is harmless — but the ref keeps the request count
     honest and matches the candidate page. */
  const attempted = useRef(false);
  useEffect(() => {
    if (attempted.current || userId.length === 0 || token.length === 0) return;
    attempted.current = true;
    confirm.mutate();
    // Fires once per mounted page; confirm.mutate is stable and deliberately not a dependency.
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
        {/* The recovery path lives here rather than only on the login screen: someone whose link
            expired arrives on this page, not that one, and sending them elsewhere to ask for a new
            link would be a dead end they have to figure out themselves. */}
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
          {/* Success is reported for any address — the endpoint deliberately cannot say whether one is
              registered — so the wording promises only that a link was sent if the account exists. */}
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

/* Shown right after registering: the workspace exists, the session does not yet. Kept separate from
   the page above because the two are different moments — one is "we mailed you something", the other
   is "you clicked it". */
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
