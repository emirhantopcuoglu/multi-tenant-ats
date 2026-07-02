import type { Role } from '@/types/enums';

/* Mirrors the backend CanManageApplications policy (Admin, Recruiter). Viewing is open to every role,
   so only the write actions (move stage, reject) gate on this. The API enforces the same policy. */
export function canManageApplications(role: Role | null): boolean {
  return role === 'Admin' || role === 'Recruiter';
}
