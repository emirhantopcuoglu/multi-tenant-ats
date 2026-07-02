import type { Role } from './enums';

/* Body for POST /api/v1/invitations (InvitationsController.InviteRequest). The tenant is taken from
   the caller's JWT, so only the invitee's email and target role are sent. The endpoint returns no
   body — the user appears in GET /users once they accept the emailed link. */
export interface InviteUserRequest {
  email: string;
  role: Role;
}
