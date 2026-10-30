import type { Role } from './enums';

/* A plain array, not paged. */
export interface TenantUser {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: Role;
  /* Deactivated users stay listed so they can be reactivated. Active users come first. */
  isActive: boolean;
}
