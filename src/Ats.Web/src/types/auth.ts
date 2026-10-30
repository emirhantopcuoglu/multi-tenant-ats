import type { Role } from './enums';

/* Same shape for company and candidate endpoints. */
export interface AuthResult {
  accessToken: string;
  refreshToken: string;
}

export type CandidateAuthResult = AuthResult;

/* `kind` is added on the client based on which /me endpoint answered. */
export interface CompanyUser {
  kind: 'company';
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: Role;
  tenant: CurrentUserTenant;
}

/* Never 'Deleted': a deleted account has no live session. */
export type CandidateAccountStatus = 'Active' | 'Frozen';

export interface CandidateUser {
  kind: 'candidate';
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  status: CandidateAccountStatus;
  /* Separate from `status`: unverified accounts can do everything except apply. */
  isEmailVerified: boolean;
}

export type CurrentUser = CompanyUser | CandidateUser;

export interface CurrentUserTenant {
  companyName: string;
  slug: string;
}

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

  /* Sent with registration because the confirmation email goes out in it. */
  preferredLanguage: string;
}

export interface CandidateLoginRequest {
  email: string;
  password: string;
}

export interface CandidateRegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;

  /* Sent with registration because the confirmation email goes out in it. */
  preferredLanguage: string;
}

/* Returns no tokens; the user goes to /login afterwards. */
export interface AcceptInvitationRequest {
  token: string;
  password: string;
  firstName: string;
  lastName: string;

  /* Sent with the request because email to this user starts right away. */
  preferredLanguage: string;
}
