import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { toApiError } from '@/lib/problemDetails';
import { AuthLayout } from '@/features/auth/components/AuthLayout';
import { authErrorMessage } from '@/features/auth/authErrorMessage';

export function CandidateLoginPage() {
  const { t } = useTranslation();
  const { candidateLogin } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [formError, setFormError] = useState<string | null>(null);

  const from = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname ?? '/';

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
      await candidateLogin(values);
      navigate(from, { replace: true });
    } catch (error) {
      setFormError(authErrorMessage(toApiError(error), t));
    }
  });

  return (
    <AuthLayout title={t('candidateAuth.loginTitle')} subtitle={t('candidateAuth.loginSub')}>
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
        {t('candidateAuth.noAccount')}{' '}
        <Link to="/candidate/register" state={location.state} className="text-accent hover:underline">
          {t('candidateAuth.createOne')}
        </Link>
      </p>
    </AuthLayout>
  );
}
