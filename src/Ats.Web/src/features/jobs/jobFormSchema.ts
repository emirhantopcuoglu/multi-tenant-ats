import { z } from 'zod';
import type { TFunction } from 'i18next';
import {
  EMPLOYMENT_TYPES,
  EXPERIENCE_LEVELS,
  type EmploymentType,
  type ExperienceLevel,
} from '@/types/enums';
import type { JobDetail, JobWriteRequest } from '@/types/job';

/* Salary inputs stay as strings in the form (native number inputs surface empty as '' rather than a
   number), and are parsed/validated together: a salary is either fully blank or fully provided. */
export interface JobFormValues {
  title: string;
  description: string;
  department: string;
  location: string;
  employmentType: EmploymentType;
  experienceLevel: ExperienceLevel;
  salaryMin: string;
  salaryMax: string;
  salaryCurrency: string;
}

const TITLE_MAX = 200;
const DEPARTMENT_MAX = 100;
const LOCATION_MAX = 200;

/* Schema built with `t` so validation messages are localized (same pattern as the auth forms).
   Description may be empty for a draft; the publish action enforces it separately in the form. */
export function buildJobSchema(t: TFunction) {
  return z
    .object({
      title: z
        .string()
        .trim()
        .min(1, t('validation.required'))
        .max(TITLE_MAX, t('jobForm.titleMax', { count: TITLE_MAX })),
      description: z.string(),
      department: z.string().max(DEPARTMENT_MAX, t('jobForm.fieldMax')),
      location: z.string().max(LOCATION_MAX, t('jobForm.fieldMax')),
      employmentType: z.enum(EMPLOYMENT_TYPES),
      experienceLevel: z.enum(EXPERIENCE_LEVELS),
      salaryMin: z.string(),
      salaryMax: z.string(),
      salaryCurrency: z.string(),
    })
    .superRefine((values, ctx) => {
      const hasAnySalary = Boolean(values.salaryMin || values.salaryMax || values.salaryCurrency);
      if (!hasAnySalary) return;

      const min = Number(values.salaryMin);
      const max = Number(values.salaryMax);
      if (!values.salaryMin || Number.isNaN(min)) {
        ctx.addIssue({ code: 'custom', path: ['salaryMin'], message: t('jobForm.salaryNumber') });
      }
      if (!values.salaryMax || Number.isNaN(max)) {
        ctx.addIssue({ code: 'custom', path: ['salaryMax'], message: t('jobForm.salaryNumber') });
      }
      if (values.salaryMin && values.salaryMax && !Number.isNaN(min) && !Number.isNaN(max) && max < min) {
        ctx.addIssue({ code: 'custom', path: ['salaryMax'], message: t('jobForm.salaryRange') });
      }
      if (!values.salaryCurrency.trim()) {
        ctx.addIssue({ code: 'custom', path: ['salaryCurrency'], message: t('jobForm.currencyRequired') });
      }
    });
}

export function emptyJobValues(): JobFormValues {
  return {
    title: '',
    description: '',
    department: '',
    location: '',
    employmentType: 'FullTime',
    experienceLevel: 'Mid',
    salaryMin: '',
    salaryMax: '',
    salaryCurrency: '',
  };
}

export function jobToValues(job: JobDetail): JobFormValues {
  return {
    title: job.title,
    description: job.description,
    department: job.department,
    location: job.location,
    employmentType: job.employmentType,
    experienceLevel: job.experienceLevel,
    salaryMin: job.salaryMin?.toString() ?? '',
    salaryMax: job.salaryMax?.toString() ?? '',
    salaryCurrency: job.salaryCurrency ?? '',
  };
}

export function valuesToRequest(values: JobFormValues): JobWriteRequest {
  const hasSalary = Boolean(values.salaryMin && values.salaryMax && values.salaryCurrency.trim());
  return {
    title: values.title.trim(),
    description: values.description,
    department: values.department.trim(),
    location: values.location.trim(),
    employmentType: values.employmentType,
    experienceLevel: values.experienceLevel,
    salaryMin: hasSalary ? Number(values.salaryMin) : null,
    salaryMax: hasSalary ? Number(values.salaryMax) : null,
    salaryCurrency: hasSalary ? values.salaryCurrency.trim().toUpperCase() : null,
  };
}
