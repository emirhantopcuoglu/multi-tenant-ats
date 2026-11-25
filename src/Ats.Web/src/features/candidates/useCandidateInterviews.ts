import { useQuery } from '@tanstack/react-query';
import { listCandidateInterviews } from './candidateInterviewsApi';

export function useCandidateInterviews(page: number, pageSize: number) {
  return useQuery({
    queryKey: ['candidate', 'interviews', page, pageSize],
    queryFn: () => listCandidateInterviews(page, pageSize),
    placeholderData: (prev) => prev,
  });
}
