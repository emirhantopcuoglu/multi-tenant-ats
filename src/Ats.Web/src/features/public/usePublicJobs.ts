import { useQuery } from '@tanstack/react-query';
import { getPublicJob, listPublicJobs } from './publicJobsApi';

/* Query keys are namespaced under 'public' so they never collide with the authenticated jobs cache
   (a recruiter could be signed in while viewing a public page). Both are keyed by slug. */
export const publicJobsKey = (slug: string) => ['public', 'jobs', slug] as const;
export const publicJobKey = (slug: string, jobSlug: string) =>
  ['public', 'job', slug, jobSlug] as const;

const PUBLIC_PAGE_SIZE = 50;

/* Published jobs for a tenant's careers page. One page is enough for an MVP careers board; if a
   tenant outgrows it, this is where pagination would be added. */
export function usePublicJobs(slug: string) {
  return useQuery({
    queryKey: publicJobsKey(slug),
    queryFn: () => listPublicJobs(slug, 1, PUBLIC_PAGE_SIZE),
    enabled: slug.length > 0,
  });
}

export function usePublicJob(slug: string, jobSlug: string) {
  return useQuery({
    queryKey: publicJobKey(slug, jobSlug),
    queryFn: () => getPublicJob(slug, jobSlug),
    enabled: slug.length > 0 && jobSlug.length > 0,
    // A 404 (unknown/unpublished job) is a final answer, not a transient failure — don't hammer it.
    retry: false,
  });
}
