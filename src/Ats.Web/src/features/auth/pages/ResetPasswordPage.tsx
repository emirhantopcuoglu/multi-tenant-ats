import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input } from '@/components/ui';
import { toApiError } from '@/lib/problemDetails';
import { AuthLayout } from '../components/AuthLayout';
import { resetPassword } from '../passwordResetApi';

/* Mirrors Identity's minimum; the server enforces it. */
const PASSWORD_MIN = 8;

/* A rejected password, which the user can fix, as opposed to a dead link. */
const PASSWORD_REJECTED_CODE = 'auth.password_rejected';

/* The token is only used on submit, so a mail scanner opening the link can't spend it. Afterwards
   the user signs in; no session is handed out. */
export function ResetPasswordPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [formError, setFormError] = useState<string | null>(null);
  const [isDone, setIsDone] = useState(false);

  const userId = searchParams.get('userId');
  const token = searchParams.get('token');

  const schema = useMemo(
    () =>
      z
        .object({
          newPassword: z
            .string()
            .min(PASSWORD_MIN, t('validation.passwordMin', { count: PASSWORD_MIN })),
          confirmPassword: z.string(),
        })
        .refine((values) => values.newPassword === values.confirmPassword, {
          path: ['confirmPassword'],
          message: t('candidateSettings.security.passwordMismatch'),
        }),
    [t],
  );
  type ResetForm = z.infer<typeof schema>;

  const { register, handleSubmit, formState } = useForm<ResetForm>({
    resolver: zodResolver(schema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await resetPassword({ userId: userId!, token: token!, newPassword: values.newPassword });
      setIsDone(true);
    } catch (error) {
      const apiError = toApiError(error);
      setFormError(
        apiError.code === PASSWORD_REJECTED_CODE
          ? (apiError.message ?? t('auth.resetPassword.invalidLink'))
          : t('auth.resetPassword.invalidLink'),
      );
    }
  });

  if (!userId || !token) {
    return (
      <AuthLayout
        title={t('auth.resetPassword.title')}
        subtitle={t('auth.resetPassword.missingToken')}
      >
        <Link to="/forgot-password" className="text-sm text-accent hover:underline">
          {t('auth.resetPassword.requestNew')}
        </Link>
      </AuthLayout>
    );
  }

  if (isDone) {
    return (
      <AuthLayout
        title={t('auth.resetPassword.doneTitle')}
        subtitle={t('auth.resetPassword.doneBody')}
      >
        <Button
          type="button"
          className="w-full"
          onClick={() => navigate('/login', { replace: true })}
        >
          {t('confirmEmailChange.goToLogin')}
        </Button>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      title={t('auth.resetPassword.title')}
      subtitle={t('auth.resetPassword.subtitle')}
    >
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        {formError && (
          <div role="alert" className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {formError}{' '}
            <Link to="/forgot-password" className="underline">
              {t('auth.resetPassword.requestNew')}
            </Link>
          </div>
        )}

        <Field
          label={t('auth.resetPassword.newPassword')}
          error={formState.errors.newPassword?.message}
        >
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              type="password"
              autoComplete="new-password"
              aria-describedby={describedById}
              invalid={invalid}
              {...register('newPassword')}
            />
          )}
        </Field>

        <Field
          label={t('auth.resetPassword.confirmPassword')}
          error={formState.errors.confirmPassword?.message}
        >
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              type="password"
              autoComplete="new-password"
              aria-describedby={describedById}
              invalid={invalid}
              {...register('confirmPassword')}
            />
          )}
        </Field>

        <Button type="submit" className="w-full" disabled={formState.isSubmitting}>
          {t('auth.resetPassword.submit')}
        </Button>
      </form>
    </AuthLayout>
  );
}
