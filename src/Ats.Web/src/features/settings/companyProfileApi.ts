import { apiClient, API_V1 } from '@/lib/apiClient';

/* Name and slug are read-only. */

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
