import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input } from '@/components/ui';
import { AuthLayout } from '@/features/auth/components/AuthLayout';
import { requestCandidatePasswordReset } from '../candidatePasswordResetApi';

/* Step one of recovery: collect an address and ask the backend to mail a link.

   The confirmation copy says "if an account exists, we sent a link" rather than "we sent you a
   link", and it is shown for every submitted address. The backend answers identically whether or not
   the email is registered; a friendlier "no account with that address" here would hand back exactly
   the account directory the endpoint is written to withhold. */
export function CandidateForgotPasswordPage() {
  const { t } = useTranslation();
  const [isSubmitted, setIsSubmitted] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const schema = useMemo(
    () => z.object({ email: z.string().email(t('validation.email')) }),
    [t],
  );
  type ForgotForm = z.infer<typeof schema>;

  const { register, handleSubmit, formState } = useForm<ForgotForm>({
    resolver: zodResolver(schema),
    defaultValues: { email: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await requestCandidatePasswordReset(values.email);
      setIsSubmitted(true);
    } catch {
      // Only a transport or rate-limit failure can land here — the endpoint reports success for
      // unknown addresses — so this is a "try again", not "no such account".
      setFormError(t('candidateAuth.forgotPassword.failed'));
    }
  });

  if (isSubmitted) {
    return (
      <AuthLayout
        title={t('candidateAuth.forgotPassword.sentTitle')}
        subtitle={t('candidateAuth.forgotPassword.sentBody')}
        audience="candidate"
      >
        <Link
          to="/candidate/login"
          className="block text-sm text-accent hover:underline"
        >
          {t('candidateAuth.forgotPassword.backToSignIn')}
        </Link>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      title={t('candidateAuth.forgotPassword.title')}
      subtitle={t('candidateAuth.forgotPassword.subtitle')}
      audience="candidate"
    >
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        {formError && (
          <div role="alert" className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {formError}
          </div>
        )}

        <Field label={t('auth.email')} error={formState.errors.email?.message}>
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              type="email"
              autoComplete="email"
              aria-describedby={describedById}
              invalid={invalid}
              {...register('email')}
            />
          )}
        </Field>

        <Button type="submit" className="w-full" disabled={formState.isSubmitting}>
          {t('candidateAuth.forgotPassword.submit')}
        </Button>
      </form>

      <p className="text-sm text-text-muted">
        <Link to="/candidate/login" className="text-accent hover:underline">
          {t('candidateAuth.forgotPassword.backToSignIn')}
        </Link>
      </p>
    </AuthLayout>
  );
}
