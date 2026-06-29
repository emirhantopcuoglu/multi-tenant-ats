import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Input } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { toApiError } from '@/lib/problemDetails';
import { AuthLayout } from '../components/AuthLayout';
import { Field } from '../components/Field';
import { authErrorMessage } from '../authErrorMessage';

export function LoginPage() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [formError, setFormError] = useState<string | null>(null);

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
    try {
      await login(values);
      navigate(from, { replace: true });
    } catch (error) {
      setFormError(authErrorMessage(toApiError(error), t));
    }
  });

  return (
    <AuthLayout title={t('auth.loginTitle')} subtitle={t('auth.loginSub', { company: t('common.appName') })}>
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        {formError && (
          <div role="alert" className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {formError}
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
