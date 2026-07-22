import type { Role } from './enums';

/* Returned by company register / login / refresh. */
export interface AuthResult {
  accessToken: string;
  refreshToken: string;
}

/* Returned by candidate register / login — access-token only (no refresh). */
export interface CandidateAuthResult {
  accessToken: string;
}

/* Discriminated union: all company-workspace users vs global candidate accounts.
   The `kind` field is added client-side when deserializing /auth/me and /candidate/auth/me
   since the backend owns no such field; the calling endpoint determines the type. */
export interface CompanyUser {
  kind: 'company';
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: Role;
  tenant: CurrentUserTenant;
}

/* What /candidate/auth/me can report. Never 'Deleted': a deleted account's token dies with the
   stamp rotation, so no signed-in session ever sees that state. */
export type CandidateAccountStatus = 'Active' | 'Frozen';

export interface CandidateUser {
  kind: 'candidate';
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  status: CandidateAccountStatus;
}

export type CurrentUser = CompanyUser | CandidateUser;

export interface CurrentUserTenant {
  companyName: string;
  slug: string;
}

/* Request bodies for company auth endpoints. */
export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  companyName: string;
  slug: string;
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

/* Request bodies for candidate auth endpoints. */
export interface CandidateLoginRequest {
  email: string;
  password: string;
}

export interface CandidateRegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

/* POST /api/v1/invitations/accept (InvitationsController.AcceptRequest). The token comes from the
   invitation URL; the user only supplies a password and their name. Unlike login/register this
   endpoint returns no tokens, so the user is sent to /login afterwards. */
export interface AcceptInvitationRequest {
  token: string;
  password: string;
  firstName: string;
  lastName: string;
}
