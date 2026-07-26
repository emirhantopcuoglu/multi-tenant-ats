import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMutation } from '@tanstack/react-query';
import { Button, Field, Input } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { toApiError } from '@/lib/problemDetails';
import { AuthLayout } from '../components/AuthLayout';
import { AudienceSwitch } from '../components/AudienceSwitch';
import { authErrorMessage, EMAIL_NOT_CONFIRMED_CODE } from '../authErrorMessage';
import { resendEmailConfirmation } from '../authApi';

export function LoginPage() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [formError, setFormError] = useState<string | null>(null);
  const [unconfirmedEmail, setUnconfirmedEmail] = useState<string | null>(null);
  const resend = useMutation({ mutationFn: (email: string) => resendEmailConfirmation(email) });

  // Where to return after login: the page the guard bounced us from, or the dashboard.
  const from = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname ?? '/';

  // Schema built with t() so validation messages are localized; memoized per language.
  const schema = useMemo(
    () =>
      z.object({
        email: z.string().email(t('validation.email')),
        password: z.string().min(1, t('validation.required')),
      }),
    [t],
  );
  type LoginForm = z.infer<typeof schema>;

  const { register, handleSubmit, formState } = useForm<LoginForm>({
    resolver: zodResolver(schema),
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    setUnconfirmedEmail(null);
    try {
      await login(values);
      navigate(from, { replace: true });
    } catch (error) {
      const apiError = toApiError(error);
      setFormError(authErrorMessage(apiError, t));
      /* The one login failure the user can act on from this screen. Their password was correct — the
         server only reaches this error after verifying it — so remembering the address to offer a
         resend reveals nothing they have not already proved they know. */
      if (apiError.code === EMAIL_NOT_CONFIRMED_CODE) setUnconfirmedEmail(values.email);
    }
  });

  return (
    <AuthLayout title={t('auth.loginTitle')} subtitle={t('auth.loginSub', { company: t('common.appName') })}>
      <AudienceSwitch active="company" variant="login" />

      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        {formError && (
          <div role="alert" className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {formError}
            {unconfirmedEmail && (
              <div className="mt-2">
                <button
                  type="button"
                  onClick={() => resend.mutate(unconfirmedEmail)}
                  disabled={resend.isPending || resend.isSuccess}
                  className="font-medium underline disabled:no-underline disabled:opacity-70"
                >
                  {resend.isSuccess
                    ? t('auth.confirmEmail.resendSent')
                    : t('auth.confirmEmail.resendAction')}
                </button>
              </div>
            )}
          </div>
        )}

        <Field label={t('auth.email')} error={formState.errors.email?.message}>
          {({ id, describedById, invalid }) => (
            <Input id={id} type="email" autoComplete="email" aria-describedby={describedById} invalid={invalid} {...register('email')} />
          )}
        </Field>

        <Field label={t('auth.password')} error={formState.errors.password?.message}>
          {({ id, describedById, invalid }) => (
            <Input id={id} type="password" autoComplete="current-password" aria-describedby={describedById} invalid={invalid} {...register('password')} />
          )}
        </Field>

        <div className="text-right">
          <Link to="/forgot-password" className="text-sm text-accent hover:underline">
            {t('auth.forgotPassword.link')}
          </Link>
        </div>

        <Button type="submit" className="w-full" disabled={formState.isSubmitting}>
          {t('auth.signInBtn')}
        </Button>
      </form>

      <p className="text-sm text-text-muted">
        {t('auth.noAccount')}{' '}
        <Link to="/register" className="text-accent hover:underline">
          {t('auth.createOne')}
        </Link>
      </p>
    </AuthLayout>
  );
}
