import { apiClient } from '@/lib/apiClient';

/* Name and email come from the candidate account. */
export interface ApplyRequest {
  phone?: string;
  linkedInUrl?: string;
  coverLetter?: string;
  /* Omit to use the account's CV; the server copies it. */
  cv?: File;
}

/* Axios sets the multipart boundary for FormData. */
export async function submitApplication(
  slug: string,
  jobSlug: string,
  request: ApplyRequest,
): Promise<{ id: string }> {
  const form = new FormData();
  if (request.phone) form.append('Phone', request.phone);
  if (request.linkedInUrl) form.append('LinkedInUrl', request.linkedInUrl);
  if (request.coverLetter) form.append('CoverLetter', request.coverLetter);
  if (request.cv) form.append('Cv', request.cv);

  const { data } = await apiClient.post<{ id: string }>(
    `/${encodeURIComponent(slug)}/jobs/${encodeURIComponent(jobSlug)}/apply`,
    form,
  );
  return data;
}
