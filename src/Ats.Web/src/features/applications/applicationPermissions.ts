import type { Role } from '@/types/enums';

/* Mirrors the backend CanManageApplications policy (Admin, Recruiter). */
export function canManageApplications(role: Role | null): boolean {
  return role === 'Admin' || role === 'Recruiter';
}
