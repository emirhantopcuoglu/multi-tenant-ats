import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link, Navigate, useLocation, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, EmptyState, Field, Input, Skeleton, Textarea } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { useAppliedJobIds } from '@/features/candidates/useAppliedJobIds';
import { toApiError } from '@/lib/problemDetails';
import { PublicLayout } from './components/PublicLayout';
import { PublicNotFound } from './components/PublicNotFound';
import { CvUpload } from './components/CvUpload';
import { usePublicJob } from './usePublicJobs';
import { useApplyToJob } from './useApplyToJob';
import { validateCvFile, type CvFileError } from './cvFile';
import {
  COVER_LETTER_MAX_LENGTH,
  LINKEDIN_URL_MAX_LENGTH,
  isAbsoluteHttpUrl,
  isPlausiblePhone,
} from './applyValidation';

const FILE_ERROR_KEYS = {
  'file.empty': 'public.apply.fileEmpty',
  'file.too_large': 'public.apply.fileTooLarge',
  'file.unsupported_type': 'public.apply.fileUnsupported',
  'file.content_mismatch': 'public.apply.fileMismatch',
} as const satisfies Record<CvFileError | 'file.content_mismatch', string>;

export function PublicApplyPage() {
  const { t } = useTranslation();
  const { slug = '', jobSlug = '' } = useParams();
  const location = useLocation();
  const { user, isLoading: authLoading } = useAuth();
  const jobQuery = usePublicJob(slug, jobSlug);
  const apply = useApplyToJob(slug, jobSlug);
  const appliedJobIds = useAppliedJobIds(user?.kind === 'candidate');

  const [cv, setCv] = useState<File | null>(null);
  const [cvError, setCvError] = useState<string | undefined>(undefined);
  const [bannerError, setBannerError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  // All three fields are optional; the refinements only fire on non-empty input. The rules
  // mirror SubmitApplicationValidator — see applyValidation.ts.
  const schema = useMemo(
    () =>
      z.object({
        phone: z
          .string()
          .trim()
          .refine((value) => value === '' || isPlausiblePhone(value), t('validation.phone')),
        linkedInUrl: z
          .string()
          .trim()
          .max(LINKEDIN_URL_MAX_LENGTH, t('validation.maxLength', { count: LINKEDIN_URL_MAX_LENGTH }))
          .refine((value) => value === '' || isAbsoluteHttpUrl(value), t('validation.url')),
        coverLetter: z
          .string()
          .trim()
          .max(COVER_LETTER_MAX_LENGTH, t('validation.maxLength', { count: COVER_LETTER_MAX_LENGTH })),
      }),
    [t],
  );
  type ApplyForm = z.infer<typeof schema>;

  const { register, handleSubmit, formState } = useForm<ApplyForm>({
    resolver: zodResolver(schema),
    defaultValues: { phone: '', linkedInUrl: '', coverLetter: '' },
  });

  // appliedJobIds.isLoading is false while the query is disabled (anonymous visitor), so this
  // only ever waits for a signed-in candidate's membership check — no cost to everyone else.
  if (authLoading || jobQuery.isLoading || appliedJobIds.isLoading) {
    return (
      <PublicLayout>
        <Skeleton className="h-64 w-full" />
      </PublicLayout>
    );
  }

  if (jobQuery.isError || !jobQuery.data) {
    return <PublicNotFound />;
  }

  // Must be a signed-in candidate to apply. Redirect to candidate login, preserving the return URL.
  if (!user || user.kind !== 'candidate') {
    return <Navigate to="/candidate/login" replace state={{ from: location }} />;
  }

  const job = jobQuery.data;

  const selectCv = (file: File) => {
    const code = validateCvFile(file);
    if (code) {
      setCv(null);
      setCvError(t(FILE_ERROR_KEYS[code]));
      return;
    }
    setCv(file);
    setCvError(undefined);
  };

  // The CV lives outside react-hook-form (it's a File in component state, not an input value),
  // so its required-check runs in both submit branches — otherwise a schema error would hide
  // the missing-CV error until the second attempt.
  const onSubmit = handleSubmit(
    (values) => {
      setBannerError(null);
      if (!cv) {
        setCvError(t('public.apply.cvRequired'));
        return;
      }
      setCvError(undefined);

      apply.mutate(
        {
          phone: values.phone || undefined,
          linkedInUrl: values.linkedInUrl || undefined,
          coverLetter: values.coverLetter || undefined,
          cv,
        },
        {
          onSuccess: () => setSubmitted(true),
          onError: (error) => mapSubmitError(error),
        },
      );
    },
    () => {
      if (!cv) setCvError(t('public.apply.cvRequired'));
    },
  );

  const mapSubmitError = (error: unknown) => {
    const { code } = toApiError(error);

    if (code in FILE_ERROR_KEYS) {
      setCvError(t(FILE_ERROR_KEYS[code as keyof typeof FILE_ERROR_KEYS]));
      return;
    }
    if (code === 'application.duplicate') {
      setBannerError(t('public.apply.duplicate'));
      return;
    }
    if (code === 'application.job_not_available' || code === 'http_404') {
      setBannerError(t('public.apply.unavailable'));
      return;
    }
    setBannerError(t('public.apply.error'));
  };

  if (submitted) {
    return (
      <PublicLayout>
        <Card className="py-12">
          <EmptyState
            title={t('public.apply.successTitle')}
            description={t('public.apply.successBody', { title: job.title })}
            action={
              <Link to={`/${slug}`} className="text-sm font-medium text-accent hover:underline">
                {t('public.apply.backToJobs')}
              </Link>
            }
          />
        </Card>
      </PublicLayout>
    );
  }

  // Already applied (and not via this visit — `submitted` above wins right after a submit, since
  // the cache invalidation would flip this branch on too): show the state instead of the form.
  if (appliedJobIds.data?.has(job.id)) {
    return (
      <PublicLayout>
        <Card className="py-12">
          <EmptyState
            title={t('public.apply.alreadyAppliedTitle')}
            description={t('public.apply.alreadyAppliedBody', { title: job.title })}
            action={
              <Link
                to="/candidate/applications"
                className="text-sm font-medium text-accent hover:underline"
              >
                {t('public.apply.viewApplications')}
              </Link>
            }
          />
        </Card>
      </PublicLayout>
    );
  }

  return (
    <PublicLayout>
      <div className="mx-auto max-w-xl space-y-6">
        <div className="space-y-1">
          <Link
            to={`/${slug}/jobs/${jobSlug}`}
            className="text-sm text-text-muted transition-colors hover:text-accent"
          >
            {t('public.apply.backToJob')}
          </Link>
          <h1 className="text-2xl font-semibold tracking-tight">
            {t('public.apply.title', { title: job.title })}
          </h1>
        </div>

        {/* Identity confirmed from the candidate account — not re-entered on the form. */}
        <div className="rounded-lg border border-border bg-card px-4 py-3 text-sm">
          <span className="text-text-muted">{t('public.apply.applyingAs')}</span>{' '}
          <span className="font-medium text-text">
            {user.firstName} {user.lastName}
          </span>
          <span className="text-text-muted"> · {user.email}</span>
        </div>

        {bannerError && (
          <div className="rounded-lg border border-danger/40 bg-danger-bg px-4 py-3 text-sm text-danger">
            {bannerError}
          </div>
        )}

        <form onSubmit={onSubmit} noValidate>
          <Card className="space-y-4">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field label={t('public.apply.phone')} error={formState.errors.phone?.message}>
                {({ id, describedById, invalid }) => (
                  <Input
                    id={id}
                    type="tel"
                    inputMode="tel"
                    autoComplete="tel"
                    placeholder={t('public.apply.phonePlaceholder')}
                    aria-describedby={describedById}
                    invalid={invalid}
                    {...register('phone')}
                  />
                )}
              </Field>
              <Field
                label={t('public.apply.linkedIn')}
                error={formState.errors.linkedInUrl?.message}
              >
                {({ id, describedById, invalid }) => (
                  <Input
                    id={id}
                    inputMode="url"
                    autoComplete="url"
                    placeholder={t('public.apply.linkedInPlaceholder')}
                    aria-describedby={describedById}
                    invalid={invalid}
                    {...register('linkedInUrl')}
                  />
                )}
              </Field>
            </div>

            <Field
              label={t('public.apply.coverLetter')}
              error={formState.errors.coverLetter?.message}
            >
              {({ id, describedById, invalid }) => (
                <Textarea
                  id={id}
                  rows={5}
                  placeholder={t('public.apply.coverLetterPlaceholder')}
                  aria-describedby={describedById}
                  invalid={invalid}
                  {...register('coverLetter')}
                />
              )}
            </Field>

            <CvUpload
              file={cv}
              onSelect={selectCv}
              onClear={() => { setCv(null); setCvError(undefined); }}
              error={cvError}
            />

            <div className="pt-2">
              <Button type="submit" disabled={apply.isPending} className="w-full">
                {apply.isPending ? t('public.apply.submitting') : t('public.apply.submit')}
              </Button>
            </div>
          </Card>
        </form>
      </div>
    </PublicLayout>
  );
}
