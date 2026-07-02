/* GET /api/v1/dashboard/stats (Ats.Api DashboardStatsDto). The four headline numbers on the Overview
   home, computed tenant-scoped per request across the Jobs, Applications, and Interviews modules. */
export interface DashboardStats {
  openJobs: number;
  newApplicationsThisWeek: number;
  upcomingInterviews: number;
  activeCandidates: number;
}
