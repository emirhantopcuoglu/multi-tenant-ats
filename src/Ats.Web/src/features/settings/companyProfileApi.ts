import { apiClient, API_V1 } from '@/lib/apiClient';

/* The tenant's own public profile (GET/PUT /api/v1/tenant/profile, Admin-only). Name and slug are
   read-only context; the nullable fields are what the admin edits and what the public careers page
   renders. Errors come back as { code, message } (e.g. tenant_profile.website_invalid). */

export interface CompanyProfile {
  companyName: string;
  slug: string;
  description: string | null;
  website: string | null;
  location: string | null;
}

export interface UpdateCompanyProfileRequest {
  description: string | null;
  website: string | null;
  location: string | null;
}

export async function getCompanyProfile(): Promise<CompanyProfile> {
  const { data } = await apiClient.get<CompanyProfile>(`${API_V1}/tenant/profile`);
  return data;
}

export async function updateCompanyProfile(
  request: UpdateCompanyProfileRequest,
): Promise<CompanyProfile> {
  const { data } = await apiClient.put<CompanyProfile>(`${API_V1}/tenant/profile`, request);
  return data;
}
