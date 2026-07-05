import { useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Button, Card, Field, Input, Skeleton, useToast } from '@/components/ui';
import { PublicLayout } from '@/features/public/components/PublicLayout';
import { useCandidateCurrentUser } from '../useCandidateCurrentUser';
import { useUpdateCandidateProfile } from '../useCandidateProfile';
import type { CandidateUser } from '@/types/auth';

/* Mirrors CandidateAccount.FirstName/LastName HasMaxLength(100) on the backend. */
const NAME_MAX_LENGTH = 100;

export function CandidateProfilePage() {
  const { t } = useTranslation();
  const userQuery = useCandidateCurrentUser();

  return (
    <PublicLayout>
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">{t('candidateProfile.title')}</h1>
          <p className="text-sm text-text-muted">{t('candidateProfile.subtitle')}</p>
        </div>

        {userQuery.isLoading ? (
          <Card className="max-w-xl space-y-4">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </Card>
        ) : userQuery.isError || !userQuery.data ? (
          <Card className="max-w-xl">
            <p className="text-sm text-text-muted">{t('candidateProfile.loadError')}</p>
          </Card>
        ) : (
          <CandidateProfileForm user={userQuery.data} />
        )}
      </div>
    </PublicLayout>
  );
}

/* Split so the form mounts only once the profile is loaded — react-hook-form reads defaultValues
   on mount, and feeding it late data would need an effect-driven reset instead (same reasoning as
   the company Settings form). */
function CandidateProfileForm({ user }: { user: CandidateUser }) {
  const { t } = useTranslation();
  const { toast } = useToast();
  const update = useUpdateCandidateProfile();

  const schema = useMemo(
    () =>
      z.object({
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
      }),
    [t],
  );
  type ProfileForm = z.infer<typeof schema>;

  const { register, handleSubmit, reset, formState } = useForm<ProfileForm>({
    resolver: zodResolver(schema),
    defaultValues: {
      firstName: user.firstName,
      lastName: user.lastName,
    },
  });

  const onSubmit = handleSubmit((form) => {
    update.mutate(form, {
      onSuccess: () => {
        toast({ title: t('candidateProfile.saved'), tone: 'success' });
        reset(form);
      },
      onError: () => {
        toast({ title: t('candidateProfile.saveError'), tone: 'danger' });
      },
    });
  });

  return (
    <Card className="max-w-xl">
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <div className="space-y-1">
          <span className="block text-sm font-medium text-text">{t('candidateProfile.email')}</span>
          <p className="text-sm text-text-muted">{user.email}</p>
          <p className="text-xs text-text-muted">{t('candidateProfile.emailReadonlyHint')}</p>
        </div>

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

        <div className="flex justify-end">
          <Button type="submit" disabled={!formState.isDirty || update.isPending}>
            {t('common.save')}
          </Button>
        </div>
      </form>
    </Card>
  );
}
