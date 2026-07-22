import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Button, Card, Field, Input, useToast } from '@/components/ui';
import { toApiError } from '@/lib/problemDetails';
import { useChangeCandidatePassword, useRequestCandidateEmailChange } from '../useCandidateProfile';

/* Mirrors CandidatePasswordPolicy.MinimumLength on the backend (UX only — the server enforces). */
const PASSWORD_MIN_LENGTH = 8;

/* The backend's typed error codes, each mapped to a field-level message instead of the generic
   failure toast — every one of them is something the caller can fix by editing the form. */
const INVALID_CURRENT_PASSWORD_CODE = 'candidate_profile.invalid_current_password';
const EMAIL_ALREADY_REGISTERED_CODE = 'candidate_profile.email_already_registered';
const EMAIL_UNCHANGED_CODE = 'candidate_profile.email_unchanged';
const INVALID_EMAIL_CODE = 'candidate_profile.invalid_email';

/* The Güvenlik tab of /candidate/settings: password change and email change, each its own card and
   its own form so neither shares dirty state or a submit button with the other. */
export function CandidateSecuritySettingsTab() {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h2 className="text-lg font-semibold tracking-tight">{t('candidateSettings.security.heading')}</h2>
        <p className="text-sm text-text-muted">{t('candidateSettings.security.subheading')}</p>
      </div>

      <PasswordChangeCard />
      <EmailChangeCard />
    </div>
  );
}

/* Submitting mails a confirmation link to the new address — the account's email itself changes only
   once that link is opened, so the success state stays on screen (not just a toast) since the user
   leaves the app to act on it. */
function EmailChangeCard() {
  const { t } = useTranslation();
  const { toast } = useToast();
  const requestEmailChange = useRequestCandidateEmailChange();
  const [pendingEmail, setPendingEmail] = useState<string | null>(null);

  const schema = useMemo(
    () =>
      z.object({
        newEmail: z.string().email(t('validation.email')),
        currentPassword: z.string().min(1, t('validation.required')),
      }),
    [t],
  );
  type EmailForm = z.infer<typeof schema>;

  const { register, handleSubmit, reset, setError, formState } = useForm<EmailForm>({
    resolver: zodResolver(schema),
    defaultValues: { newEmail: '', currentPassword: '' },
  });

  const onSubmit = handleSubmit((form) => {
    requestEmailChange.mutate(
      { newEmail: form.newEmail, currentPassword: form.currentPassword },
      {
        onSuccess: () => {
          setPendingEmail(form.newEmail);
          toast({ title: t('candidateSettings.security.emailRequested'), tone: 'success' });
          reset();
        },
        onError: (error) => {
          const code = toApiError(error).code;
          if (code === INVALID_CURRENT_PASSWORD_CODE) {
            setError('currentPassword', { message: t('candidateSettings.currentPasswordInvalid') });
          } else if (code === EMAIL_ALREADY_REGISTERED_CODE) {
            setError('newEmail', { message: t('candidateSettings.security.emailTaken') });
          } else if (code === EMAIL_UNCHANGED_CODE) {
            setError('newEmail', { message: t('candidateSettings.security.emailUnchanged') });
          } else if (code === INVALID_EMAIL_CODE) {
            setError('newEmail', { message: t('candidateSettings.security.emailInvalid') });
          } else {
            toast({ title: t('candidateSettings.security.emailChangeError'), tone: 'danger' });
          }
        },
      },
    );
  });

  return (
    <Card className="max-w-xl">
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <div className="space-y-1">
          <h3 className="text-lg font-medium">{t('candidateSettings.security.emailTitle')}</h3>
          <p className="text-sm text-text-muted">{t('candidateSettings.security.emailSubtitle')}</p>
        </div>

        {pendingEmail && (
          <p className="rounded-lg bg-info-bg px-3 py-2 text-sm text-info">
            {t('candidateSettings.security.emailRequestedHint', { email: pendingEmail })}
          </p>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t('candidateSettings.security.newEmail')} error={formState.errors.newEmail?.message}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="email"
                autoComplete="email"
                aria-describedby={describedById}
                invalid={invalid}
                {...register('newEmail')}
              />
            )}
          </Field>

          <Field
            label={t('candidateSettings.currentPassword')}
            error={formState.errors.currentPassword?.message}
          >
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="password"
                autoComplete="current-password"
                aria-describedby={describedById}
                invalid={invalid}
                {...register('currentPassword')}
              />
            )}
          </Field>
        </div>

        <div className="flex justify-end">
          <Button type="submit" disabled={requestEmailChange.isPending}>
            {t('candidateSettings.security.emailSubmit')}
          </Button>
        </div>
      </form>
    </Card>
  );
}

function PasswordChangeCard() {
  const { t } = useTranslation();
  const { toast } = useToast();
  const changePassword = useChangeCandidatePassword();

  const schema = useMemo(
    () =>
      z
        .object({
          currentPassword: z.string().min(1, t('validation.required')),
          newPassword: z
            .string()
            .min(PASSWORD_MIN_LENGTH, t('validation.passwordMin', { count: PASSWORD_MIN_LENGTH })),
          confirmPassword: z.string(),
        })
        .superRefine((values, ctx) => {
          if (values.confirmPassword !== values.newPassword) {
            ctx.addIssue({
              code: 'custom',
              path: ['confirmPassword'],
              message: t('candidateSettings.security.passwordMismatch'),
            });
          }
        }),
    [t],
  );
  type PasswordForm = z.infer<typeof schema>;

  const { register, handleSubmit, reset, setError, formState } = useForm<PasswordForm>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  });

  const onSubmit = handleSubmit((form) => {
    changePassword.mutate(
      { currentPassword: form.currentPassword, newPassword: form.newPassword },
      {
        onSuccess: () => {
          toast({ title: t('candidateSettings.security.passwordChanged'), tone: 'success' });
          reset();
        },
        onError: (error) => {
          /* A wrong current password is the caller's own typo — point at the field. Anything
             else (network, server) gets the generic failure toast. */
          if (toApiError(error).code === INVALID_CURRENT_PASSWORD_CODE) {
            setError('currentPassword', {
              message: t('candidateSettings.currentPasswordInvalid'),
            });
          } else {
            toast({ title: t('candidateSettings.security.passwordChangeError'), tone: 'danger' });
          }
        },
      },
    );
  });

  return (
    <Card className="max-w-xl">
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <div className="space-y-1">
          <h3 className="text-lg font-medium">{t('candidateSettings.security.passwordTitle')}</h3>
          <p className="text-sm text-text-muted">{t('candidateSettings.security.passwordSubtitle')}</p>
        </div>

        <Field
          label={t('candidateSettings.currentPassword')}
          error={formState.errors.currentPassword?.message}
        >
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              type="password"
              autoComplete="current-password"
              aria-describedby={describedById}
              invalid={invalid}
              {...register('currentPassword')}
            />
          )}
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field
            label={t('candidateSettings.security.newPassword')}
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
            label={t('candidateSettings.security.confirmPassword')}
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
        </div>

        <div className="flex justify-end">
          <Button type="submit" disabled={changePassword.isPending}>
            {t('candidateSettings.security.passwordSubmit')}
          </Button>
        </div>
      </form>
    </Card>
  );
}
