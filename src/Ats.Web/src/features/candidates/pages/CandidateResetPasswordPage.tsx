import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input } from '@/components/ui';
import { AuthLayout } from '@/features/auth/components/AuthLayout';
import { resetCandidatePassword } from '../candidatePasswordResetApi';

/* Mirrors CandidatePasswordPolicy.MinimumLength; the server is the enforcement point, this is UX. */
const PASSWORD_MIN = 8;

/* Step two of recovery: the landing page for the mailed link.

   No auto-submit on mount, unlike nothing here but worth stating: the token is only spent when the
   candidate submits a password, so a corporate mail scanner that opens the link (the hazard
   ConfirmEmailChangePage documents) cannot burn it.

   On success the candidate is sent to sign in rather than being logged in here. The reset revoked
   every session by rotating the security stamp, so there is no session to continue — and a reset
   that handed out a session would reward anyone who merely guessed a token. */
export function CandidateResetPasswordPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [formError, setFormError] = useState<string | null>(null);
  const [isDone, setIsDone] = useState(false);

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
      await resetCandidatePassword({ token: token!, newPassword: values.newPassword });
      setIsDone(true);
    } catch {
      // The backend collapses unknown, expired and already-used into one answer, so this message
      // has to cover all three rather than guess which applies.
      setFormError(t('candidateAuth.resetPassword.invalidLink'));
    }
  });

  if (!token) {
    return (
      <AuthLayout
        title={t('candidateAuth.resetPassword.title')}
        subtitle={t('candidateAuth.resetPassword.missingToken')}
        audience="candidate"
      >
        <Link to="/candidate/forgot-password" className="text-sm text-accent hover:underline">
          {t('candidateAuth.resetPassword.requestNew')}
        </Link>
      </AuthLayout>
    );
  }

  if (isDone) {
    return (
      <AuthLayout
        title={t('candidateAuth.resetPassword.doneTitle')}
        subtitle={t('candidateAuth.resetPassword.doneBody')}
        audience="candidate"
      >
        <Button
          type="button"
          className="w-full"
          onClick={() => navigate('/candidate/login', { replace: true })}
        >
          {t('confirmEmailChange.goToLogin')}
        </Button>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      title={t('candidateAuth.resetPassword.title')}
      subtitle={t('candidateAuth.resetPassword.subtitle')}
      audience="candidate"
    >
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        {formError && (
          <div role="alert" className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {formError}{' '}
            <Link to="/candidate/forgot-password" className="underline">
              {t('candidateAuth.resetPassword.requestNew')}
            </Link>
          </div>
        )}

        <Field
          label={t('candidateAuth.resetPassword.newPassword')}
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
          label={t('candidateAuth.resetPassword.confirmPassword')}
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
          {t('candidateAuth.resetPassword.submit')}
        </Button>
      </form>
    </AuthLayout>
  );
}
