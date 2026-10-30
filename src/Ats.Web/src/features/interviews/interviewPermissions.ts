import type { Role } from '@/types/enums';

/* Mirrors CanManageInterviews, which unlike applications includes hiring managers. */
export function canManageInterviews(role: Role | null): boolean {
  return role === 'Admin' || role === 'Recruiter' || role === 'HiringManager';
}
