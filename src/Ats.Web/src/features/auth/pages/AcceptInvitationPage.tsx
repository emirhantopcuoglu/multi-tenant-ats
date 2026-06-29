import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input, useToast } from '@/components/ui';
import { acceptInvitation } from '@/features/auth/authApi';
import { toApiError } from '@/lib/problemDetails';
import { AuthLayout } from '../components/AuthLayout';
import { authErrorMessage } from '../authErrorMessage';

const PASSWORD_MIN = 8;

export function AcceptInvitationPage() {
  const { t } = useTranslation();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { toast } = useToast();
  const [formError, setFormError] = useState<string | null>(null);

  const token = searchParams.get('token');

  const schema = useMemo(
    () =>
      z.object({
        firstName: z.string().min(1, t('validation.required')),
        lastName: z.string().min(1, t('validation.required')),
        password: z.string().min(PASSWORD_MIN, t('validation.passwordMin', { count: PASSWORD_MIN })),
      }),
    [t],
  );
  type AcceptForm = z.infer<typeof schema>;

  const { register, handleSubmit, formState } = useForm<AcceptForm>({
    resolver: zodResolver(schema),
  });

  const onSubmit = handleSubmit(async (values) => {
    if (!token) return;
    setFormError(null);
    try {
      await acceptInvitation({ token, ...values });
      // The endpoint returns no tokens, so the user isn't signed in — confirm and send them to login.
      toast({ title: t('auth.invitationAccepted'), tone: 'success' });
      navigate('/login', { replace: true });
    } catch (error) {
      setFormError(authErrorMessage(toApiError(error), t));
    }
  });

  if (!token) {
    return (
      <AuthLayout title={t('auth.invTitle')} subtitle={t('auth.missingToken')}>
        <span />
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title={t('auth.invTitle')} subtitle={t('auth.invSub')}>
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

        <Field label={t('auth.setPassword')} error={formState.errors.password?.message}>
          {({ id, describedById, invalid }) => (
            <Input id={id} type="password" autoComplete="new-password" aria-describedby={describedById} invalid={invalid} {...register('password')} />
          )}
        </Field>
        <p className="-mt-2 text-xs text-text-muted">{t('auth.pwHint')}</p>

        <Button type="submit" className="w-full" disabled={formState.isSubmitting}>
          {t('auth.acceptBtn')}
        </Button>
      </form>
    </AuthLayout>
  );
}
