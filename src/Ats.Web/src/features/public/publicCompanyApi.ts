import { apiClient } from '@/lib/apiClient';

/* 404 for unknown slugs. Null fields were never filled in. */

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
