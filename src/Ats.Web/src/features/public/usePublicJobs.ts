import { useQuery } from '@tanstack/react-query';
import { getPublicJob, listPublicJobs } from './publicJobsApi';

/* Under 'public' so they don't collide with the authenticated jobs cache. */
export const publicJobsKey = (slug: string) => ['public', 'jobs', slug] as const;
export const publicJobKey = (slug: string, jobSlug: string) =>
  ['public', 'job', slug, jobSlug] as const;

const PUBLIC_PAGE_SIZE = 50;

/* One page is enough for now. */
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
    // A 404 is final; don't retry.
    retry: false,
  });
}
