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
import { AudienceSwitch } from '@/features/auth/components/AudienceSwitch';
import { authErrorMessage } from '@/features/auth/authErrorMessage';

const PASSWORD_MIN = 8;

export function CandidateRegisterPage() {
  const { t } = useTranslation();
  const { candidateRegister } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [formError, setFormError] = useState<string | null>(null);

  const from = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname ?? '/';

  const schema = useMemo(
    () =>
      z.object({
        firstName: z.string().min(1, t('validation.required')),
        lastName: z.string().min(1, t('validation.required')),
        email: z.string().email(t('validation.email')),
        password: z.string().min(PASSWORD_MIN, t('validation.passwordMin', { count: PASSWORD_MIN })),
      }),
    [t],
  );
  type RegisterForm = z.infer<typeof schema>;

  const { register, handleSubmit, formState } = useForm<RegisterForm>({
    resolver: zodResolver(schema),
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await candidateRegister(values);
      navigate(from, { replace: true });
    } catch (error) {
      setFormError(authErrorMessage(toApiError(error), t));
    }
  });

  return (
    <AuthLayout
      title={t('candidateAuth.registerTitle')}
      subtitle={t('candidateAuth.registerSub')}
      audience="candidate"
    >
      <AudienceSwitch active="candidate" variant="register" state={location.state} />

      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        {formError && (
          <div role="alert" className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {formError}
          </div>
        )}

        <div className="grid grid-cols-2 gap-3">
          <Field label={t('auth.firstName')} error={formState.errors.firstName?.message}>
            {({ id, describedById, invalid }) => (
              <Input id={id} autoComplete="given-name" aria-describedby={describedById} invalid={invalid} {...register('firstName')} />
            )}
          </Field>
          <Field label={t('auth.lastName')} error={formState.errors.lastName?.message}>
            {({ id, describedById, invalid }) => (
              <Input id={id} autoComplete="family-name" aria-describedby={describedById} invalid={invalid} {...register('lastName')} />
            )}
          </Field>
        </div>

        <Field label={t('auth.email')} error={formState.errors.email?.message}>
          {({ id, describedById, invalid }) => (
            <Input id={id} type="email" autoComplete="email" aria-describedby={describedById} invalid={invalid} {...register('email')} />
          )}
        </Field>

        <Field label={t('auth.password')} error={formState.errors.password?.message}>
          {({ id, describedById, invalid }) => (
            <Input id={id} type="password" autoComplete="new-password" aria-describedby={describedById} invalid={invalid} {...register('password')} />
          )}
        </Field>
        <p className="-mt-2 text-xs text-text-muted">{t('auth.pwHint')}</p>

        <Button type="submit" className="w-full" disabled={formState.isSubmitting}>
          {t('candidateAuth.createBtn')}
        </Button>
      </form>

      <p className="text-sm text-text-muted">
        {t('candidateAuth.haveAccount')}{' '}
        <Link to="/candidate/login" state={location.state} className="text-accent hover:underline">
          {t('candidateAuth.signInLink')}
        </Link>
      </p>
    </AuthLayout>
  );
}
