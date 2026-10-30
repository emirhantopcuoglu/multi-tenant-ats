import { apiClient, API_V1 } from '@/lib/apiClient';

/* Email is read-only here; it has its own verified change flow. birthDate is "yyyy-MM-dd". */

/* Null when no CV is attached. */
export interface CandidateCv {
  fileName: string;
  uploadedAtUtc: string;
}

export interface CandidateProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  country: string | null;
  city: string | null;
  birthDate: string | null;
  cv: CandidateCv | null;
}

export interface UpdateCandidateProfileRequest {
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  country: string | null;
  city: string | null;
  birthDate: string | null;
}

export async function getCandidateProfile(): Promise<CandidateProfile> {
  const { data } = await apiClient.get<CandidateProfile>(`${API_V1}/candidate/profile`);
  return data;
}

export async function updateCandidateProfile(
  request: UpdateCandidateProfileRequest,
): Promise<CandidateProfile> {
  const { data } = await apiClient.put<CandidateProfile>(`${API_V1}/candidate/profile`, request);
  return data;
}

export interface ChangeCandidatePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

/* The stamp rotation kills the current tokens; the caller must store the new pair right away. */
export interface ChangeCandidatePasswordResult {
  accessToken: string;
  refreshToken: string;
}

export async function changeCandidatePassword(
  request: ChangeCandidatePasswordRequest,
): Promise<ChangeCandidatePasswordResult> {
  const { data } = await apiClient.post<ChangeCandidatePasswordResult>(
    `${API_V1}/candidate/profile/password`,
    request,
  );
  return data;
}

/* Request mails a link to the new address; confirm (anonymous) applies it and ends every session. */
export interface RequestCandidateEmailChangeRequest {
  newEmail: string;
  currentPassword: string;
}

export async function requestCandidateEmailChange(
  request: RequestCandidateEmailChangeRequest,
): Promise<void> {
  await apiClient.post(`${API_V1}/candidate/profile/email`, request);
}

export async function confirmCandidateEmailChange(token: string): Promise<void> {
  await apiClient.post(`${API_V1}/candidate/profile/email/confirm`, { token });
}

/* Uploading replaces the current CV. */
export async function uploadCandidateCv(file: File): Promise<CandidateCv> {
  const form = new FormData();
  form.append('file', file);

  /* No Content-Type: the browser must set it so the multipart boundary is included. */
  const { data } = await apiClient.post<CandidateCv>(`${API_V1}/candidate/profile/cv`, form);
  return data;
}

export async function removeCandidateCv(): Promise<void> {
  await apiClient.delete(`${API_V1}/candidate/profile/cv`);
}

/* Fetch at click time; the signed URL expires quickly. */
export interface CandidateCvDownload {
  url: string;
  expiresInSeconds: number;
}

export async function getCandidateCvDownloadUrl(): Promise<CandidateCvDownload> {
  const { data } = await apiClient.get<CandidateCvDownload>(
    `${API_V1}/candidate/profile/cv/download-url`,
  );
  return data;
}
