import type { Role } from './enums';

/* Returned by register / login / refresh on both sides. The candidate endpoints return the same
   shape as the company ones, so the refresh interceptor handles either with one type. */
export interface AuthResult {
  accessToken: string;
  refreshToken: string;
}

export type CandidateAuthResult = AuthResult;

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
  /* Whether the candidate has clicked the link mailed to this address. Unverified accounts are fully
     usable except for applying — deliberately not folded into `status`, which answers a different
     question (Active/Frozen/Deleted). */
  isEmailVerified: boolean;
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

  /* The UI language at the moment of signing up. The server stores it and writes every later email
     in it — including the confirmation mail this request triggers, which is why it has to travel
     here rather than being set from the settings screen afterwards. */
  preferredLanguage: string;
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

  /* The UI language at the moment of signing up. The server stores it and writes every later email
     in it — including the confirmation mail this request triggers, which is why it has to travel
     here rather than being set from the settings screen afterwards. */
  preferredLanguage: string;
}

/* POST /api/v1/invitations/accept (InvitationsController.AcceptRequest). The token comes from the
   invitation URL; the user only supplies a password and their name. Unlike login/register this
   endpoint returns no tokens, so the user is sent to /login afterwards. */
export interface AcceptInvitationRequest {
  token: string;
  password: string;
  firstName: string;
  lastName: string;

  /* The UI language at the moment of signing up. The server stores it and writes every later email
     in it — including the confirmation mail this request triggers, which is why it has to travel
     here rather than being set from the settings screen afterwards. */
  preferredLanguage: string;
}
