import type { Role } from '@/types/enums';

/* Mirrors the backend CanManageJobs policy (Admin, Recruiter). */
export function canManageJobs(role: Role | null): boolean {
  return role === 'Admin' || role === 'Recruiter';
}
