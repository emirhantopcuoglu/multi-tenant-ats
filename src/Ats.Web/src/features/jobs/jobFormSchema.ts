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
import { CITIES_BY_COUNTRY, COUNTRIES, type Country } from '@/types/location';
import type { JobDetail, JobWriteRequest } from '@/types/job';

/* Salary fields stay strings ('' when empty) and are all-or-nothing. salaryCurrency uses the
   dropdown's union so zodResolver's type matches. City is a string because its options depend on
   the country. */
export interface JobFormValues {
  title: string;
  description: string;
  department: string;
  city: string;
  country: Country | '';
  employmentType: EmploymentType;
  experienceLevel: ExperienceLevel;
  workArrangement: WorkArrangement;
  salaryMin: string;
  salaryMax: string;
  salaryCurrency: Currency | '';
}

const TITLE_MAX = 200;
const DEPARTMENT_MAX = 100;

/* Built with t() so messages are translated. Drafts may have no description; publish checks it. */
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
      // '' means no country yet; the city is checked in superRefine.
      city: z.string(),
      country: z.enum(['', ...COUNTRIES]),
      employmentType: z.enum(EMPLOYMENT_TYPES),
      experienceLevel: z.enum(EXPERIENCE_LEVELS),
      workArrangement: z.enum(WORK_ARRANGEMENTS),
      salaryMin: z.string(),
      salaryMax: z.string(),
      // Optional unless a salary is set.
      salaryCurrency: z.enum(['', ...CURRENCIES]),
    })
    .superRefine((values, ctx) => {
      // Always required, unlike salaryCurrency.
      if (!values.country) {
        ctx.addIssue({ code: 'custom', path: ['country'], message: t('jobForm.countryRequired') });
      } else if (!CITIES_BY_COUNTRY[values.country].includes(values.city)) {
        ctx.addIssue({ code: 'custom', path: ['city'], message: t('jobForm.cityRequired') });
      }

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
    // Display-only cast: an old value outside the list just shows a blank select.
    country: (job.country as Country | null) ?? '',
    employmentType: job.employmentType,
    experienceLevel: job.experienceLevel,
    workArrangement: job.workArrangement,
    salaryMin: job.salaryMin?.toString() ?? '',
    salaryMax: job.salaryMax?.toString() ?? '',
    // Display-only cast: an old value outside the list just shows a blank select.
    salaryCurrency: (job.salaryCurrency as Currency | null) ?? '',
  };
}

export function valuesToRequest(values: JobFormValues): JobWriteRequest {
  const hasSalary = Boolean(values.salaryMin && values.salaryMax && values.salaryCurrency);
  return {
    title: values.title.trim(),
    description: values.description,
    department: values.department.trim(),
    city: values.city,
    country: values.country || null,
    employmentType: values.employmentType,
    experienceLevel: values.experienceLevel,
    workArrangement: values.workArrangement,
    salaryMin: hasSalary ? Number(values.salaryMin) : null,
    salaryMax: hasSalary ? Number(values.salaryMax) : null,
    salaryCurrency: hasSalary ? values.salaryCurrency : null,
  };
}
