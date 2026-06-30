import type { Role } from '@/types/enums';

/* Mirrors the backend CanManageInterviews policy. Note this differs from applications: hiring managers
   run interviews, so they can schedule and manage them here — whereas managing applications is limited
   to Admin and Recruiter. Viewing is open to every role, so only the write actions gate on this.
   The API enforces the same policy; this only governs whether the UI offers the controls. */
export function canManageInterviews(role: Role | null): boolean {
  return role === 'Admin' || role === 'Recruiter' || role === 'HiringManager';
}
