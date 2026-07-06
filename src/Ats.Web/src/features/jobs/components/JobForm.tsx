import { useMemo } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslation } from 'react-i18next';
import { Button, Card, Field, Input, Select } from '@/components/ui';
import { CURRENCIES, EMPLOYMENT_TYPES, EXPERIENCE_LEVELS, WORK_ARRANGEMENTS } from '@/types/enums';
import { buildJobSchema, type JobFormValues } from '../jobFormSchema';
import { MarkdownField } from './MarkdownField';

interface JobFormProps {
  defaultValues: JobFormValues;
  mode: 'create' | 'edit';
  /** Show the Publish button (create, or editing a still-draft job). */
  showPublish: boolean;
  submitting: boolean;
  onSubmit: (values: JobFormValues, publish: boolean) => void;
  onCancel: () => void;
}

/* Create/edit form, shared by both routes. The two submit buttons reuse one handleSubmit with a
   `publish` flag; Publish additionally requires a non-empty description (a draft does not). */
export function JobForm({ defaultValues, mode, showPublish, submitting, onSubmit, onCancel }: JobFormProps) {
  const { t } = useTranslation();
  const schema = useMemo(() => buildJobSchema(t), [t]);
  const { register, handleSubmit, control, formState, setError } = useForm<JobFormValues>({
    resolver: zodResolver(schema),
    defaultValues,
  });
  const { errors } = formState;

  const submit = (publish: boolean) =>
    handleSubmit((values) => {
      if (publish && !values.description.trim()) {
        setError('description', { message: t('jobForm.descriptionRequiredToPublish') });
        return;
      }
      onSubmit(values, publish);
    });

  const saveLabel = mode === 'create' ? t('jobForm.saveDraft') : t('jobForm.saveChanges');

  return (
    <Card className="space-y-5">
      <Field label={t('jobForm.title')} error={errors.title?.message}>
        {({ id, describedById, invalid }) => (
          <Input id={id} aria-describedby={describedById} invalid={invalid} {...register('title')} />
        )}
      </Field>

      <Field label={t('jobForm.description')} error={errors.description?.message}>
        {({ id, describedById, invalid }) => (
          <Controller
            control={control}
            name="description"
            render={({ field }) => (
              <MarkdownField
                id={id}
                value={field.value}
                onChange={field.onChange}
                invalid={invalid}
                describedById={describedById}
                placeholder={t('jobForm.descriptionHint')}
              />
            )}
          />
        )}
      </Field>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={t('jobForm.department')} error={errors.department?.message}>
          {({ id, describedById, invalid }) => (
            <Input id={id} aria-describedby={describedById} invalid={invalid} {...register('department')} />
          )}
        </Field>
        <Field label={t('jobForm.city')} error={errors.city?.message}>
          {({ id, describedById, invalid }) => (
            <Input id={id} aria-describedby={describedById} invalid={invalid} {...register('city')} />
          )}
        </Field>
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={t('jobForm.country')} error={errors.country?.message}>
          {({ id, describedById, invalid }) => (
            <Input id={id} aria-describedby={describedById} invalid={invalid} {...register('country')} />
          )}
        </Field>
        <Field label={t('jobForm.workArrangement')} error={errors.workArrangement?.message}>
          {({ id, describedById, invalid }) => (
            <Select id={id} aria-describedby={describedById} invalid={invalid} {...register('workArrangement')}>
              {WORK_ARRANGEMENTS.map((value) => (
                <option key={value} value={value}>
                  {t(`workArrangement.${value}`)}
                </option>
              ))}
            </Select>
          )}
        </Field>
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={t('jobForm.employmentType')} error={errors.employmentType?.message}>
          {({ id, describedById, invalid }) => (
            <Select id={id} aria-describedby={describedById} invalid={invalid} {...register('employmentType')}>
              {EMPLOYMENT_TYPES.map((value) => (
                <option key={value} value={value}>
                  {t(`employmentType.${value}`)}
                </option>
              ))}
            </Select>
          )}
        </Field>
        <Field label={t('jobForm.experienceLevel')} error={errors.experienceLevel?.message}>
          {({ id, describedById, invalid }) => (
            <Select id={id} aria-describedby={describedById} invalid={invalid} {...register('experienceLevel')}>
              {EXPERIENCE_LEVELS.map((value) => (
                <option key={value} value={value}>
                  {t(`experienceLevel.${value}`)}
                </option>
              ))}
            </Select>
          )}
        </Field>
      </div>

      <div className="space-y-1.5">
        <span className="block text-sm font-medium text-text">{t('jobForm.salary')}</span>
        <div className="grid grid-cols-3 gap-3">
          <Input
            type="number"
            aria-label={t('jobForm.salaryMin')}
            placeholder={t('jobForm.salaryMin')}
            invalid={Boolean(errors.salaryMin)}
            {...register('salaryMin')}
          />
          <Input
            type="number"
            aria-label={t('jobForm.salaryMax')}
            placeholder={t('jobForm.salaryMax')}
            invalid={Boolean(errors.salaryMax)}
            {...register('salaryMax')}
          />
          <Select
            aria-label={t('jobForm.currency')}
            invalid={Boolean(errors.salaryCurrency)}
            {...register('salaryCurrency')}
          >
            <option value="">{t('jobForm.currencyPlaceholder')}</option>
            {CURRENCIES.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </Select>
        </div>
        {(errors.salaryMin || errors.salaryMax || errors.salaryCurrency) && (
          <p className="text-xs text-danger">
            {errors.salaryMin?.message ?? errors.salaryMax?.message ?? errors.salaryCurrency?.message}
          </p>
        )}
      </div>

      <div className="flex justify-end gap-2 border-t border-divider pt-4">
        <Button variant="ghost" onClick={onCancel} disabled={submitting}>
          {t('common.cancel')}
        </Button>
        <Button variant="secondary" onClick={submit(false)} disabled={submitting}>
          {saveLabel}
        </Button>
        {showPublish && (
          <Button onClick={submit(true)} disabled={submitting}>
            {t('jobForm.publish')}
          </Button>
        )}
      </div>
    </Card>
  );
}
