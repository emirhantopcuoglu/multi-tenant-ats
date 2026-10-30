import type { EmploymentType, ExperienceLevel, WorkArrangement } from './enums';

/* Includes the company name and slug so items can link to the careers page. */
export interface MarketplaceJob {
  id: string;
  title: string;
  companyName: string;
  companySlug: string;
  city: string;
  country: string | null;
  employmentType: EmploymentType;
  experienceLevel: ExperienceLevel;
  workArrangement: WorkArrangement;
  slug: string;
  publishedAtUtc: string | null;
}
