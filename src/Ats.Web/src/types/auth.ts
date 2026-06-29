import type { Role } from './enums';

/* Returned by register / login / refresh (Tenants.Application.AuthResult). */
export interface AuthResult {
  accessToken: string;
  refreshToken: string;
}

/* GET /api/v1/auth/me (Tenants.Application.CurrentUserDto). The JWT lacks the display name and
   company name, so the topbar and role-based UI read them from here. */
export interface CurrentUser {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: Role;
  tenant: CurrentUserTenant;
}

export interface CurrentUserTenant {
  companyName: string;
  slug: string;
}

/* Request bodies for the auth endpoints (AuthController nested records). */
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

/* POST /api/v1/invitations/accept (InvitationsController.AcceptRequest). The token comes from the
   invitation URL; the user only supplies a password and their name. Unlike login/register this
   endpoint returns no tokens, so the user is sent to /login afterwards. */
export interface AcceptInvitationRequest {
  token: string;
  password: string;
  firstName: string;
  lastName: string;
}
