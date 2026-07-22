import { apiClient } from '@/lib/apiClient';

/* Fields submitted with a job application. Identity (name, email) is omitted — the backend reads
   those from the CandidateAccount linked to the JWT. Only the supplemental fields remain. */
export interface ApplyRequest {
  phone?: string;
  linkedInUrl?: string;
  coverLetter?: string;
  cv: File;
}

/* Submits a candidate application as multipart/form-data. The endpoint is gated behind the
   CandidateOnly policy, so the apiClient request interceptor attaches the candidate access token
   automatically. Axios sets the multipart boundary header when handed a FormData. */
export async function submitApplication(
  slug: string,
  jobSlug: string,
  request: ApplyRequest,
): Promise<{ id: string }> {
  const form = new FormData();
  if (request.phone) form.append('Phone', request.phone);
  if (request.linkedInUrl) form.append('LinkedInUrl', request.linkedInUrl);
  if (request.coverLetter) form.append('CoverLetter', request.coverLetter);
  form.append('Cv', request.cv);

  const { data } = await apiClient.post<{ id: string }>(
    `/${encodeURIComponent(slug)}/jobs/${encodeURIComponent(jobSlug)}/apply`,
    form,
  );
  return data;
}
