import { useMemo, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { toApiError } from '@/lib/problemDetails';
import { slugify } from '@/lib/slugify';
import { AuthLayout } from '../components/AuthLayout';
import { authErrorMessage } from '../authErrorMessage';

const PASSWORD_MIN = 8;
// Display-only host for the workspace URL preview; the real public host is configured at deploy time.
const WORKSPACE_HOST = 'ats.app';
const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export function RegisterPage() {
  const { t } = useTranslation();
  const { register: registerUser } = useAuth();
  const navigate = useNavigate();
  const [formError, setFormError] = useState<string | null>(null);

  const schema = useMemo(
    () =>
      z.object({
        companyName: z.string().min(1, t('validation.required')),
        slug: z.string().regex(SLUG_PATTERN, t('validation.slugFormat')),
        firstName: z.string().min(1, t('validation.required')),
        lastName: z.string().min(1, t('validation.required')),
        email: z.string().email(t('validation.email')),
        password: z.string().min(PASSWORD_MIN, t('validation.passwordMin', { count: PASSWORD_MIN })),
      }),
    [t],
  );
  type RegisterForm = z.infer<typeof schema>;

  const { register, control, handleSubmit, watch, formState } = useForm<RegisterForm>({
    resolver: zodResolver(schema),
    defaultValues: { slug: '' },
  });

  const slug = watch('slug');

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await registerUser(values);
      navigate('/', { replace: true });
    } catch (error) {
      setFormError(authErrorMessage(toApiError(error), t));
    }
  });

  return (
    <AuthLayout title={t('auth.regTitle')} subtitle={t('auth.regSub')}>
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        {formError && (
          <div role="alert" className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {formError}
          </div>
        )}

        <Field label={t('auth.companyName')} error={formState.errors.companyName?.message}>
          {({ id, describedById, invalid }) => (
            <Input id={id} aria-describedby={describedById} invalid={invalid} {...register('companyName')} />
          )}
        </Field>

        <Field label={t('auth.workspaceUrl')} error={formState.errors.slug?.message}>
          {({ id, describedById, invalid }) => (
            <Controller
              name="slug"
              control={control}
              render={({ field }) => (
                <Input
                  id={id}
                  placeholder={t('auth.slugPlaceholder')}
                  aria-describedby={describedById}
                  invalid={invalid}
                  value={field.value}
                  onBlur={field.onBlur}
                  // Normalize as the user types, so the stored slug matches the live preview.
                  onChange={(e) => field.onChange(slugify(e.target.value))}
                />
              )}
            />
          )}
        </Field>
        <p className="-mt-2 text-xs text-text-muted">
          {t('auth.urlPreview')} <span className="font-medium text-text">{WORKSPACE_HOST}/{slug || t('auth.slugPlaceholder')}</span>
        </p>

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
          {t('auth.createBtn')}
        </Button>
      </form>

      <p className="text-sm text-text-muted">
        {t('auth.haveAccount')}{' '}
        <Link to="/login" className="text-accent hover:underline">
          {t('auth.signInLink')}
        </Link>
      </p>
    </AuthLayout>
  );
}
