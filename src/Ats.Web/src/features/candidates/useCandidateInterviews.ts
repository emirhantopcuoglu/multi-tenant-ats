import { useQuery } from '@tanstack/react-query';
import { listCandidateInterviews } from './candidateInterviewsApi';

export function useCandidateInterviews() {
  return useQuery({
    queryKey: ['candidate', 'interviews'],
    queryFn: listCandidateInterviews,
  });
}
