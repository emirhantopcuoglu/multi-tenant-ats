import { apiClient } from '@/lib/apiClient';

/* The fields a candidate submits. The CV is a real File; the rest are plain text. Mirrors the
   backend ApplyController.ApplyForm — field names are PascalCase to match its model binding. */
export interface ApplyRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  linkedInUrl?: string;
  coverLetter?: string;
  cv: File;
}

/* Submits a public job application as multipart/form-data. Axios sets the multipart boundary header
   automatically when handed a FormData. The endpoint lives at the URL root (tenant from the slug),
   is anonymous, and returns 201 with the new application id. */
export async function submitApplication(
  slug: string,
  jobSlug: string,
  request: ApplyRequest,
): Promise<{ id: string }> {
  const form = new FormData();
  form.append('FirstName', request.firstName);
  form.append('LastName', request.lastName);
  form.append('Email', request.email);
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
