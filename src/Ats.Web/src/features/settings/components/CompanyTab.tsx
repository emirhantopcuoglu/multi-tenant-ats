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

/* Mirror Tenant.*MaxLength on the backend. */
const DESCRIPTION_MAX_LENGTH = 2000;
const WEBSITE_MAX_LENGTH = 300;
const LOCATION_MAX_LENGTH = 200;

/* Loads /tenant/profile rather than the auth context, which is a login-time snapshot. */
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

/* The form mounts only after the profile loads, because react-hook-form reads defaultValues once. */
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
    // The API uses null for cleared, the form uses ''.
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
