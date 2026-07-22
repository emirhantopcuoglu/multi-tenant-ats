import { apiClient } from '@/lib/apiClient';

/* GET /public/companies/{slug} — the single-company public profile behind the careers page header.
   Unknown slugs are a 404, so the page can distinguish "no such company" from "no open roles".
   Null fields were never filled in by the company; the page hides those sections. */

export interface PublicCompanyProfile {
  companyName: string;
  slug: string;
  description: string | null;
  website: string | null;
  location: string | null;
  openJobCount: number;
}

export async function getPublicCompany(slug: string): Promise<PublicCompanyProfile> {
  const { data } = await apiClient.get<PublicCompanyProfile>(
    `/public/companies/${encodeURIComponent(slug)}`,
  );
  return data;
}
