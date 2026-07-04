import { useQuery } from '@tanstack/react-query';
import { getCandidateApplication } from './candidateApplicationsApi';

export function useCandidateApplication(id: string) {
  return useQuery({
    queryKey: ['candidate', 'application', id],
    queryFn: () => getCandidateApplication(id),
    enabled: id.length > 0,
    // A 404 (foreign or unknown id) is a final answer, not a transient failure.
    retry: false,
  });
}
