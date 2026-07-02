import type { EmploymentType, ExperienceLevel } from './enums';

/* Mirrors PublicJobFeedItemDto. Unlike the recruiter-facing Job type, each item carries the
   company name and slug so the marketplace can link to the company's own careers page. */
export interface MarketplaceJob {
  id: string;
  title: string;
  companyName: string;
  companySlug: string;
  location: string;
  employmentType: EmploymentType;
  experienceLevel: ExperienceLevel;
  slug: string;
  publishedAtUtc: string | null;
}
