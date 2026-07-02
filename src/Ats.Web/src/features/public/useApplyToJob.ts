import { useMutation } from '@tanstack/react-query';
import { submitApplication, type ApplyRequest } from './applyApi';

/* Application submission. There's no client-side cache to invalidate — an anonymous candidate has no
   list of their own applications — so this is a bare mutation; the page maps the result to a success
   state or an error message. */
export function useApplyToJob(slug: string, jobSlug: string) {
  return useMutation({
    mutationFn: (request: ApplyRequest) => submitApplication(slug, jobSlug, request),
  });
}
