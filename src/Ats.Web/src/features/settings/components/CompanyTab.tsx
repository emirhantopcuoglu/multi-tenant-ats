import { useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Button, Card, Field, Input, Skeleton, Textarea, useToast } from '@/components/ui';
import { CareersPageLink } from '@/components/CareersPageLink';
import { toApiError } from '@/lib/problemDetails';
import { isAbsoluteHttpUrl } from '@/lib/validation';
import type { CompanyProfile } from '../companyProfileApi';
import { useCompanyProfile, useUpdateCompanyProfile } from '../useCompanyProfile';

/* Mirrors of Tenant.*MaxLength on the backend — keep in sync when either changes. */
const DESCRIPTION_MAX_LENGTH = 2000;
const WEBSITE_MAX_LENGTH = 300;
const LOCATION_MAX_LENGTH = 200;

/* Company tab. Name and slug stay read-only (set at registration, no update path); the public
   profile fields — description, website, location — are editable and feed the careers page header.
   The data comes from /tenant/profile rather than the auth context: the form needs the current
   saved values, not the login-time snapshot. */
export function CompanyTab() {
  const { t } = useTranslation();
  const profileQuery = useCompanyProfile();

  if (profileQuery.isLoading) {
    return (
      <Card className="max-w-xl space-y-4">
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-10 w-full" />
      </Card>
    );
  }

  if (profileQuery.isError || !profileQuery.data) {
    return (
      <Card className="max-w-xl">
        <p className="text-sm text-text-muted">{t('settings.company.loadError')}</p>
      </Card>
    );
  }

  return <CompanyProfileForm profile={profileQuery.data} />;
}

/* Split so the form mounts only once the profile is loaded — react-hook-form reads defaultValues
   on mount, and feeding it late data would need an effect-driven reset instead. */
function CompanyProfileForm({ profile }: { profile: CompanyProfile }) {
  const { t } = useTranslation();
  const { toast } = useToast();
  const update = useUpdateCompanyProfile();

  const schema = useMemo(
    () =>
      z.object({
        description: z
          .string()
          .trim()
          .max(DESCRIPTION_MAX_LENGTH, t('validation.maxLength', { count: DESCRIPTION_MAX_LENGTH })),
        website: z
          .string()
          .trim()
          .max(WEBSITE_MAX_LENGTH, t('validation.maxLength', { count: WEBSITE_MAX_LENGTH }))
          .refine((value) => value === '' || isAbsoluteHttpUrl(value), t('validation.url')),
        location: z
          .string()
          .trim()
          .max(LOCATION_MAX_LENGTH, t('validation.maxLength', { count: LOCATION_MAX_LENGTH })),
      }),
    [t],
  );
  type ProfileForm = z.infer<typeof schema>;

  const { register, handleSubmit, reset, setError, formState } = useForm<ProfileForm>({
    resolver: zodResolver(schema),
    defaultValues: {
      description: profile.description ?? '',
      website: profile.website ?? '',
      location: profile.location ?? '',
    },
  });

  const onSubmit = handleSubmit((form) => {
    // The API models "cleared" as null, the form as '' — translate at the boundary.
    update.mutate(
      {
        description: form.description || null,
        website: form.website || null,
        location: form.location || null,
      },
      {
        onSuccess: () => {
          toast({ title: t('settings.company.saved'), tone: 'success' });
          reset(form);
        },
        onError: (error) => {
          const { code } = toApiError(error);
          if (code === 'tenant_profile.website_invalid') {
            setError('website', { message: t('validation.url') });
            return;
          }
          toast({ title: t('settings.company.saveError'), tone: 'danger' });
        },
      },
    );
  });

  return (
    <Card className="max-w-xl">
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <ReadonlyRow label={t('settings.company.name')} value={profile.companyName} />
        <ReadonlyRow label={t('settings.company.slug')} value={profile.slug} mono />
        <p className="text-xs text-text-muted">{t('settings.company.readonlyHint')}</p>

        <Field
          label={t('settings.company.description')}
          error={formState.errors.description?.message}
        >
          {({ id, describedById, invalid }) => (
            <Textarea
              id={id}
              rows={5}
              aria-describedby={describedById}
              invalid={invalid}
              placeholder={t('settings.company.descriptionPlaceholder')}
              {...register('description')}
            />
          )}
        </Field>

        <Field label={t('settings.company.website')} error={formState.errors.website?.message}>
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              type="url"
              inputMode="url"
              aria-describedby={describedById}
              invalid={invalid}
              placeholder="https://example.com"
              {...register('website')}
            />
          )}
        </Field>

        <Field label={t('settings.company.location')} error={formState.errors.location?.message}>
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              aria-describedby={describedById}
              invalid={invalid}
              placeholder={t('settings.company.locationPlaceholder')}
              {...register('location')}
            />
          )}
        </Field>

        <div className="flex items-center justify-between gap-4">
          <CareersPageLink slug={profile.slug} />
          <Button type="submit" disabled={!formState.isDirty || update.isPending}>
            {t('common.save')}
          </Button>
        </div>
      </form>
    </Card>
  );
}

function ReadonlyRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="space-y-1">
      <span className="block text-sm font-medium text-text">{label}</span>
      <p className={mono ? 'font-mono text-sm text-text-muted' : 'text-sm text-text-muted'}>{value}</p>
    </div>
  );
}
