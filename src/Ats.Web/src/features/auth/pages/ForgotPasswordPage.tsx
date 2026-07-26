import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input } from '@/components/ui';
import { AuthLayout } from '../components/AuthLayout';
import { requestPasswordReset } from '../passwordResetApi';

/* Company-side step one of recovery. Same anti-enumeration copy as the candidate page: the
   confirmation says "if an account exists" and is shown for every submitted address, because the
   backend answers identically either way and a friendlier message here would leak who works where. */
export function ForgotPasswordPage() {
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
      await requestPasswordReset(values.email);
      setIsSubmitted(true);
    } catch {
      // Only transport or rate-limit failures reach here — the endpoint reports success for unknown
      // addresses — so this is "try again", not "no such account".
      setFormError(t('auth.forgotPassword.failed'));
    }
  });

  if (isSubmitted) {
    return (
      <AuthLayout
        title={t('auth.forgotPassword.sentTitle')}
        subtitle={t('auth.forgotPassword.sentBody')}
      >
        <Link to="/login" className="block text-sm text-accent hover:underline">
          {t('auth.forgotPassword.backToSignIn')}
        </Link>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      title={t('auth.forgotPassword.title')}
      subtitle={t('auth.forgotPassword.subtitle')}
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
          {t('auth.forgotPassword.submit')}
        </Button>
      </form>

      <p className="text-sm text-text-muted">
        <Link to="/login" className="text-accent hover:underline">
          {t('auth.forgotPassword.backToSignIn')}
        </Link>
      </p>
    </AuthLayout>
  );
}
