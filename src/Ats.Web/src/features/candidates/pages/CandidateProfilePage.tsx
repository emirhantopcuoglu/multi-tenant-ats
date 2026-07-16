import { useMemo, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Button, Card, Field, Input, Select, Skeleton, useToast } from '@/components/ui';
import { PublicLayout } from '@/features/public/components/PublicLayout';
import { toApiError } from '@/lib/problemDetails';
import { CITIES_BY_COUNTRY, COUNTRIES, type Country } from '@/types/location';
import {
  useCandidateProfile,
  useChangeCandidatePassword,
  useRequestCandidateEmailChange,
  useUpdateCandidateProfile,
} from '../useCandidateProfile';
import { useDeleteCandidateAccount, useFreezeCandidateAccount } from '../useCandidateAccount';
import type { CandidateProfile } from '../candidateProfileApi';
import { PhoneInput } from '../components/PhoneInput';

/* Mirrors CandidateAccount.FirstName/LastName HasMaxLength(100) on the backend. */
const NAME_MAX_LENGTH = 100;

/* Mirrors CandidatePasswordPolicy.MinimumLength on the backend (UX only — the server enforces). */
const PASSWORD_MIN_LENGTH = 8;

/* The backend's typed error codes, each mapped to a field-level message instead of the generic
   failure toast — every one of them is something the caller can fix by editing the form. */
const INVALID_CURRENT_PASSWORD_CODE = 'candidate_profile.invalid_current_password';
const EMAIL_ALREADY_REGISTERED_CODE = 'candidate_profile.email_already_registered';
const EMAIL_UNCHANGED_CODE = 'candidate_profile.email_unchanged';
const INVALID_EMAIL_CODE = 'candidate_profile.invalid_email';
const DELETE_INVALID_PASSWORD_CODE = 'candidate_account.invalid_current_password';

/* Mirrors the backend's E.164 window. The masked input already guarantees the charset and caps
   the maximum per dial country, so the only thing left to validate is "too few digits". */
const PHONE_MIN_DIGITS = 7;

/* Mirror CandidateAccount.MinimumAgeYears/MaximumAgeYears on the backend. */
const MINIMUM_AGE_YEARS = 15;
const MAXIMUM_AGE_YEARS = 100;

/* ISO yyyy-MM-dd strings order the same lexicographically as chronologically, so the allowed
   birth-date window is just two strings compared with < and >. */
function isoDateYearsAgo(years: number): string {
  const date = new Date();
  date.setFullYear(date.getFullYear() - years);
  return date.toISOString().slice(0, 10);
}

export function CandidateProfilePage() {
  const { t } = useTranslation();
  const profileQuery = useCandidateProfile();

  return (
    <PublicLayout>
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">{t('candidateProfile.title')}</h1>
          <p className="text-sm text-text-muted">{t('candidateProfile.subtitle')}</p>
        </div>

        {profileQuery.isLoading ? (
          <Card className="max-w-xl space-y-4">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </Card>
        ) : profileQuery.isError || !profileQuery.data ? (
          <Card className="max-w-xl">
            <p className="text-sm text-text-muted">{t('candidateProfile.loadError')}</p>
          </Card>
        ) : (
          <>
            <CandidateProfileForm profile={profileQuery.data} />
            <EmailChangeCard />
            <PasswordChangeCard />
            <AccountCard />
          </>
        )}
      </div>
    </PublicLayout>
  );
}

/* Same structure as PasswordChangeCard: its own card, its own form, its own submit. Submitting
   changes nothing yet — it mails a confirmation link to the new address, so the success state is
   "go check that inbox", kept on screen (not just a toast) because the user leaves the app to act
   on it. Lands in the Security tab when the settings layout arrives. */
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
          toast({ title: t('candidateProfile.emailRequested'), tone: 'success' });
          reset();
        },
        onError: (error) => {
          const code = toApiError(error).code;
          if (code === INVALID_CURRENT_PASSWORD_CODE) {
            setError('currentPassword', { message: t('candidateProfile.currentPasswordInvalid') });
          } else if (code === EMAIL_ALREADY_REGISTERED_CODE) {
            setError('newEmail', { message: t('candidateProfile.emailTaken') });
          } else if (code === EMAIL_UNCHANGED_CODE) {
            setError('newEmail', { message: t('candidateProfile.emailUnchanged') });
          } else if (code === INVALID_EMAIL_CODE) {
            setError('newEmail', { message: t('candidateProfile.emailInvalid') });
          } else {
            toast({ title: t('candidateProfile.emailChangeError'), tone: 'danger' });
          }
        },
      },
    );
  });

  return (
    <Card className="max-w-xl">
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <div className="space-y-1">
          <h2 className="text-lg font-medium">{t('candidateProfile.emailTitle')}</h2>
          <p className="text-sm text-text-muted">{t('candidateProfile.emailSubtitle')}</p>
        </div>

        {pendingEmail && (
          <p className="rounded-lg bg-info-bg px-3 py-2 text-sm text-info">
            {t('candidateProfile.emailRequestedHint', { email: pendingEmail })}
          </p>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t('candidateProfile.newEmail')} error={formState.errors.newEmail?.message}>
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
            label={t('candidateProfile.currentPassword')}
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
            {t('candidateProfile.emailSubmit')}
          </Button>
        </div>
      </form>
    </Card>
  );
}

/* Its own card and its own form on purpose: the password change shares no state with the profile
   form (separate submit, separate dirty state), and merging them would make one Save button do two
   unrelated things. Lands in the Security tab when the settings layout arrives. */
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
              message: t('candidateProfile.passwordMismatch'),
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
          toast({ title: t('candidateProfile.passwordChanged'), tone: 'success' });
          reset();
        },
        onError: (error) => {
          /* A wrong current password is the caller's own typo — point at the field. Anything
             else (network, server) gets the generic failure toast. */
          if (toApiError(error).code === INVALID_CURRENT_PASSWORD_CODE) {
            setError('currentPassword', {
              message: t('candidateProfile.currentPasswordInvalid'),
            });
          } else {
            toast({ title: t('candidateProfile.passwordChangeError'), tone: 'danger' });
          }
        },
      },
    );
  });

  return (
    <Card className="max-w-xl">
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <div className="space-y-1">
          <h2 className="text-lg font-medium">{t('candidateProfile.passwordTitle')}</h2>
          <p className="text-sm text-text-muted">{t('candidateProfile.passwordSubtitle')}</p>
        </div>

        <Field
          label={t('candidateProfile.currentPassword')}
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
            label={t('candidateProfile.newPassword')}
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
            label={t('candidateProfile.confirmPassword')}
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
            {t('candidateProfile.passwordSubmit')}
          </Button>
        </div>
      </form>
    </Card>
  );
}

/* The account's danger zone: freeze (reversible, one click undoes it from the reactivation screen)
   and delete (permanent — the backend anonymizes the row, so there is nothing to restore). Delete
   demands the current password because the backend does; the form exists to collect it, not as a
   UX flourish. Lands in the Account tab when the settings layout arrives. */
function AccountCard() {
  const { t } = useTranslation();
  const { toast } = useToast();
  const freeze = useFreezeCandidateAccount();
  const deleteAccount = useDeleteCandidateAccount();

  const schema = useMemo(
    () => z.object({ currentPassword: z.string().min(1, t('validation.required')) }),
    [t],
  );
  type DeleteForm = z.infer<typeof schema>;

  const { register, handleSubmit, setError, formState } = useForm<DeleteForm>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '' },
  });

  const onDelete = handleSubmit((form) => {
    deleteAccount.mutate(
      { currentPassword: form.currentPassword },
      {
        onSuccess: () => {
          /* The success hook already ended the session and routed to the login page; the toast is
             the only trace of why. */
          toast({ title: t('candidateProfile.deleted'), tone: 'success' });
        },
        onError: (error) => {
          if (toApiError(error).code === DELETE_INVALID_PASSWORD_CODE) {
            setError('currentPassword', { message: t('candidateProfile.currentPasswordInvalid') });
          } else {
            toast({ title: t('candidateProfile.deleteError'), tone: 'danger' });
          }
        },
      },
    );
  });

  return (
    <Card className="max-w-xl space-y-6">
      <div className="space-y-1">
        <h2 className="text-lg font-medium">{t('candidateProfile.accountTitle')}</h2>
      </div>

      <div className="space-y-2">
        <h3 className="text-sm font-medium">{t('candidateProfile.freezeTitle')}</h3>
        <p className="text-sm text-text-muted">{t('candidateProfile.freezeDescription')}</p>
        <Button
          type="button"
          variant="secondary"
          disabled={freeze.isPending}
          onClick={() =>
            freeze.mutate(undefined, {
              onError: () => toast({ title: t('candidateProfile.freezeError'), tone: 'danger' }),
            })
          }
        >
          {t('candidateProfile.freezeSubmit')}
        </Button>
      </div>

      <form onSubmit={onDelete} noValidate className="space-y-2 border-t border-border pt-4">
        <h3 className="text-sm font-medium text-danger">{t('candidateProfile.deleteTitle')}</h3>
        <p className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
          {t('candidateProfile.deleteWarning')}
        </p>

        <Field
          label={t('candidateProfile.currentPassword')}
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

        <div className="flex justify-end">
          <Button type="submit" variant="danger" disabled={deleteAccount.isPending}>
            {t('candidateProfile.deleteSubmit')}
          </Button>
        </div>
      </form>
    </Card>
  );
}

/* Split so the form mounts only once the profile is loaded — react-hook-form reads defaultValues
   on mount, and feeding it late data would need an effect-driven reset instead (same reasoning as
   the company Settings form). */
function CandidateProfileForm({ profile }: { profile: CandidateProfile }) {
  const { t } = useTranslation();
  const { toast } = useToast();
  const update = useUpdateCandidateProfile();

  const latestAllowedBirthDate = isoDateYearsAgo(MINIMUM_AGE_YEARS);
  const earliestAllowedBirthDate = isoDateYearsAgo(MAXIMUM_AGE_YEARS);

  const schema = useMemo(
    () =>
      z
        .object({
          firstName: z
            .string()
            .trim()
            .min(1, t('validation.required'))
            .max(NAME_MAX_LENGTH, t('validation.maxLength', { count: NAME_MAX_LENGTH })),
          lastName: z
            .string()
            .trim()
            .min(1, t('validation.required'))
            .max(NAME_MAX_LENGTH, t('validation.maxLength', { count: NAME_MAX_LENGTH })),
          phoneNumber: z.string(),
          country: z.string(),
          city: z.string(),
          birthDate: z.string(),
        })
        .superRefine((values, ctx) => {
          const phoneDigits = values.phoneNumber.replace(/\D/g, '');
          if (phoneDigits.length > 0 && phoneDigits.length < PHONE_MIN_DIGITS) {
            ctx.addIssue({
              code: 'custom',
              path: ['phoneNumber'],
              message: t('candidateProfile.phoneInvalid'),
            });
          }
          if (values.country && !values.city) {
            ctx.addIssue({ code: 'custom', path: ['city'], message: t('validation.required') });
          }
          if (values.birthDate && values.birthDate > latestAllowedBirthDate) {
            ctx.addIssue({
              code: 'custom',
              path: ['birthDate'],
              message: t('candidateProfile.birthDateTooYoung', { age: MINIMUM_AGE_YEARS }),
            });
          }
          if (values.birthDate && values.birthDate < earliestAllowedBirthDate) {
            ctx.addIssue({
              code: 'custom',
              path: ['birthDate'],
              message: t('candidateProfile.birthDateInvalid'),
            });
          }
        }),
    [t, latestAllowedBirthDate, earliestAllowedBirthDate],
  );
  type ProfileForm = z.infer<typeof schema>;

  const { register, control, handleSubmit, reset, watch, setValue, formState } = useForm<ProfileForm>({
    resolver: zodResolver(schema),
    defaultValues: {
      firstName: profile.firstName,
      lastName: profile.lastName,
      phoneNumber: profile.phoneNumber ?? '',
      country: profile.country ?? '',
      city: profile.city ?? '',
      birthDate: profile.birthDate ?? '',
    },
  });

  /* Which cities are offered depends on the selected country, so we read it live with watch().
     The country <Select>'s onChange also clears the city field — otherwise switching country
     could leave a city selected that doesn't belong to it (same pattern as JobForm). */
  const selectedCountry = watch('country');
  const availableCities = selectedCountry
    ? (CITIES_BY_COUNTRY[selectedCountry as Country] ?? [])
    : [];

  const onSubmit = handleSubmit((form) => {
    update.mutate(
      {
        firstName: form.firstName,
        lastName: form.lastName,
        phoneNumber: form.phoneNumber.trim() || null,
        country: form.country || null,
        city: form.country ? form.city || null : null,
        birthDate: form.birthDate || null,
      },
      {
        onSuccess: () => {
          toast({ title: t('candidateProfile.saved'), tone: 'success' });
          reset(form);
        },
        onError: () => {
          toast({ title: t('candidateProfile.saveError'), tone: 'danger' });
        },
      },
    );
  });

  return (
    <Card className="max-w-xl">
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <div className="space-y-1">
          <span className="block text-sm font-medium text-text">{t('candidateProfile.email')}</span>
          <p className="text-sm text-text-muted">{profile.email}</p>
          <p className="text-xs text-text-muted">{t('candidateProfile.emailReadonlyHint')}</p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t('candidateProfile.firstName')} error={formState.errors.firstName?.message}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                aria-describedby={describedById}
                invalid={invalid}
                {...register('firstName')}
              />
            )}
          </Field>

          <Field label={t('candidateProfile.lastName')} error={formState.errors.lastName?.message}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                aria-describedby={describedById}
                invalid={invalid}
                {...register('lastName')}
              />
            )}
          </Field>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          {/* A masked, controlled component can't go through register() (that API hands out an
              uncontrolled ref); Controller is react-hook-form's bridge for exactly this case. */}
          <Field label={t('candidateProfile.phone')} error={formState.errors.phoneNumber?.message}>
            {({ id, describedById, invalid }) => (
              <Controller
                control={control}
                name="phoneNumber"
                render={({ field }) => (
                  <PhoneInput
                    id={id}
                    value={field.value}
                    onChange={field.onChange}
                    describedById={describedById}
                    invalid={invalid}
                  />
                )}
              />
            )}
          </Field>

          <Field label={t('candidateProfile.birthDate')} error={formState.errors.birthDate?.message}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="date"
                min={earliestAllowedBirthDate}
                max={latestAllowedBirthDate}
                aria-describedby={describedById}
                invalid={invalid}
                {...register('birthDate')}
              />
            )}
          </Field>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t('candidateProfile.country')} error={formState.errors.country?.message}>
            {({ id, describedById, invalid }) => (
              <Select
                id={id}
                aria-describedby={describedById}
                invalid={invalid}
                {...register('country', { onChange: () => setValue('city', '') })}
              >
                <option value="">{t('candidateProfile.countryPlaceholder')}</option>
                {COUNTRIES.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </Select>
            )}
          </Field>

          <Field label={t('candidateProfile.city')} error={formState.errors.city?.message}>
            {({ id, describedById, invalid }) => (
              <Select
                id={id}
                aria-describedby={describedById}
                invalid={invalid}
                disabled={!selectedCountry}
                {...register('city')}
              >
                <option value="">{t('candidateProfile.cityPlaceholder')}</option>
                {availableCities.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </Select>
            )}
          </Field>
        </div>

        <div className="flex justify-end">
          <Button type="submit" disabled={!formState.isDirty || update.isPending}>
            {t('common.save')}
          </Button>
        </div>
      </form>
    </Card>
  );
}
