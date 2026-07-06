import type { EmploymentType, ExperienceLevel, WorkArrangement } from './enums';

/* Mirrors PublicJobFeedItemDto. Unlike the recruiter-facing Job type, each item carries the
   company name and slug so the marketplace can link to the company's own careers page. */
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
