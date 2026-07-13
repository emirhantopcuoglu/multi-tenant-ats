import { useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Button, Card, Field, Input, Select, Skeleton, useToast } from '@/components/ui';
import { PublicLayout } from '@/features/public/components/PublicLayout';
import { CITIES_BY_COUNTRY, COUNTRIES, type Country } from '@/types/location';
import { useCandidateProfile, useUpdateCandidateProfile } from '../useCandidateProfile';
import type { CandidateProfile } from '../candidateProfileApi';

/* Mirrors CandidateAccount.FirstName/LastName HasMaxLength(100) on the backend. */
const NAME_MAX_LENGTH = 100;

/* Loose on purpose: people type local formatting ("+90 (532) 123 45 67"); the backend strips it
   and enforces E.164's 7-15 digit rule, so the client only rejects obvious non-phones. */
const PHONE_PATTERN = /^\+?[0-9 ().-]{7,20}$/;

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
          <CandidateProfileForm profile={profileQuery.data} />
        )}
      </div>
    </PublicLayout>
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
          phoneNumber: z
            .string()
            .trim()
            .regex(PHONE_PATTERN, t('candidateProfile.phoneInvalid'))
            .or(z.literal('')),
          country: z.string(),
          city: z.string(),
          birthDate: z.string(),
        })
        .superRefine((values, ctx) => {
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

  const { register, handleSubmit, reset, watch, setValue, formState } = useForm<ProfileForm>({
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
          <Field label={t('candidateProfile.phone')} error={formState.errors.phoneNumber?.message}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="tel"
                aria-describedby={describedById}
                invalid={invalid}
                {...register('phoneNumber')}
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
