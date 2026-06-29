import type { Role } from '@/types/enums';

/* Mirrors the backend CanManageJobs policy (Admin, Recruiter). Viewing the list is open to every
   role, so only the write actions (create/edit/publish/close/archive) gate on this. UI gating is a
   UX convenience — the API enforces the same policy regardless. */
export function canManageJobs(role: Role | null): boolean {
  return role === 'Admin' || role === 'Recruiter';
}
