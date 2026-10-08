import { apiClient, API_V1 } from '@/lib/apiClient';

/* GET/PUT /api/v1/candidate/profile — the dedicated profile resource, richer than GET .../me
   (which stays a minimal "who am I" for the auth context). Email is read-only here (login
   identity; changing it gets its own verified flow later). birthDate travels as a "yyyy-MM-dd"
   string: the backend stores a DateOnly, so there is no time zone to reason about. Errors come
   back as { code, message }. */

/* Null when no CV is attached. One object rather than two nullable fields, so a caller cannot
   render half of it: either there is a CV with a name and a date, or there is nothing. */
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

/* The change rotates the account's security stamp, which kills every issued token — including the
   access token this request was made with AND the refresh token behind it. The fresh pair in the
   response is what keeps the candidate's own session alive; the caller must store it immediately. */
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

/* Two-phase email change. Phase one (authenticated) mails a verification link to the NEW address;
   nothing changes until that link is used. Phase two (anonymous — the link may be opened on a
   device with no session) posts the token back. A successful confirm rotates the security stamp,
   so every session dies and the candidate logs in again with the new address. */
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

/* The CV saved to the account, reused on every application so the file only has to be found once.
   Uploading replaces whatever was there; the server deletes the displaced object. */
export async function uploadCandidateCv(file: File): Promise<CandidateCv> {
  const form = new FormData();
  form.append('file', file);

  /* No explicit Content-Type header: the browser has to set it itself so the multipart boundary
     token is included. Writing 'multipart/form-data' by hand omits the boundary and the request
     fails to parse server-side. */
  const { data } = await apiClient.post<CandidateCv>(`${API_V1}/candidate/profile/cv`, form);
  return data;
}

export async function removeCandidateCv(): Promise<void> {
  await apiClient.delete(`${API_V1}/candidate/profile/cv`);
}

/* A short-lived signed URL straight to object storage: the file never streams through the API, so
   the link is fetched at click time rather than held in component state until it expires. */
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
