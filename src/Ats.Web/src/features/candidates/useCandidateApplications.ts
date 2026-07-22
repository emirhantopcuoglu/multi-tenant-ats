import { useQuery } from '@tanstack/react-query';
import { listCandidateApplications } from './candidateApplicationsApi';

export function useCandidateApplications(page: number, pageSize = 20) {
  return useQuery({
    queryKey: ['candidate', 'applications', page, pageSize],
    queryFn: () => listCandidateApplications(page, pageSize),
    placeholderData: (prev) => prev,
  });
}
