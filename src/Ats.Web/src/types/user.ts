import type { Role } from './enums';

/* A member of the caller's tenant (Tenants.Application.TenantUserDto), from GET /api/v1/users.
   Used to resolve interviewer ids to names (list avatars, detail panel) and to populate the
   interviewer multi-select when scheduling an interview. The endpoint returns a plain array — it is
   small (one tenant's members) and not paginated. */
export interface TenantUser {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: Role;
}
