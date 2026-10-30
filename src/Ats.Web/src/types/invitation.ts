import type { Role } from './enums';

/* The tenant comes from the token. No response body. */
export interface InviteUserRequest {
  email: string;
  role: Role;
}
