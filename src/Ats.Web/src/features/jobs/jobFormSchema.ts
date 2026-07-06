import { z } from 'zod';
import type { TFunction } from 'i18next';
import {
  CURRENCIES,
  EMPLOYMENT_TYPES,
  EXPERIENCE_LEVELS,
  WORK_ARRANGEMENTS,
  type Currency,
  type EmploymentType,
  type ExperienceLevel,
  type WorkArrangement,
} from '@/types/enums';
import type { JobDetail, JobWriteRequest } from '@/types/job';

/* Salary inputs stay as strings in the form (native number inputs surface empty as '' rather than a
   number), and are parsed/validated together: a salary is either fully blank or fully provided.
   salaryCurrency narrows to the dropdown's own literal union (matching the zod schema below) rather
   than a plain string, or zodResolver's inferred resolver type stops matching JobFormValues. City
   replaces the old single free-text location field (backend renamed Location -> City); Country is
   a second, optional free-text field rather than a fixed dropdown -- unlike currency's 4 codes,
   there is no honestly "common" bounded list of countries, so a fixed list would just be the same
   restrictiveness problem the currency dropdown was designed to avoid. */
export interface JobFormValues {
  title: string;
  description: string;
  department: string;
  city: string;
  country: string;
  employmentType: EmploymentType;
  experienceLevel: ExperienceLevel;
  workArrangement: WorkArrangement;
  salaryMin: string;
  salaryMax: string;
  salaryCurrency: Currency | '';
}

const TITLE_MAX = 200;
const DEPARTMENT_MAX = 100;
const CITY_MAX = 200;
const COUNTRY_MAX = 100;

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
      city: z.string().max(CITY_MAX, t('jobForm.fieldMax')),
      country: z.string().max(COUNTRY_MAX, t('jobForm.fieldMax')),
      employmentType: z.enum(EMPLOYMENT_TYPES),
      experienceLevel: z.enum(EXPERIENCE_LEVELS),
      workArrangement: z.enum(WORK_ARRANGEMENTS),
      salaryMin: z.string(),
      salaryMax: z.string(),
      // '' represents "no currency picked yet" -- the field is optional unless a salary is set.
      salaryCurrency: z.enum(['', ...CURRENCIES]),
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
      if (!values.salaryCurrency) {
        ctx.addIssue({ code: 'custom', path: ['salaryCurrency'], message: t('jobForm.currencyRequired') });
      }
    });
}

export function emptyJobValues(): JobFormValues {
  return {
    title: '',
    description: '',
    department: '',
    city: '',
    country: '',
    employmentType: 'FullTime',
    experienceLevel: 'Mid',
    workArrangement: 'OnSite',
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
    city: job.city,
    country: job.country ?? '',
    employmentType: job.employmentType,
    experienceLevel: job.experienceLevel,
    workArrangement: job.workArrangement,
    salaryMin: job.salaryMin?.toString() ?? '',
    salaryMax: job.salaryMax?.toString() ?? '',
    // Cast is safe for display only: a legacy row outside the fixed list just won't match any
    // <option> and the select shows blank -- it never throws.
    salaryCurrency: (job.salaryCurrency as Currency | null) ?? '',
  };
}

export function valuesToRequest(values: JobFormValues): JobWriteRequest {
  const hasSalary = Boolean(values.salaryMin && values.salaryMax && values.salaryCurrency);
  return {
    title: values.title.trim(),
    description: values.description,
    department: values.department.trim(),
    city: values.city.trim(),
    country: values.country.trim() || null,
    employmentType: values.employmentType,
    experienceLevel: values.experienceLevel,
    workArrangement: values.workArrangement,
    salaryMin: hasSalary ? Number(values.salaryMin) : null,
    salaryMax: hasSalary ? Number(values.salaryMax) : null,
    salaryCurrency: hasSalary ? values.salaryCurrency : null,
  };
}
