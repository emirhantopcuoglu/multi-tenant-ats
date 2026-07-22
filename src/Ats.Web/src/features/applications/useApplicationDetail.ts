import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  correctApplicationStage,
  getApplication,
  getApplicationActivities,
  getCvParseResult,
  hireApplication,
  moveApplicationStage,
  rejectApplication,
} from './applicationsApi';

export const applicationDetailKey = (id: string) => ['applications', 'detail', id] as const;
const activitiesKey = (id: string) => ['applications', 'activities', id] as const;
const cvParseKey = (id: string) => ['applications', 'cv-parse', id] as const;

export function useApplication(id: string) {
  return useQuery({ queryKey: applicationDetailKey(id), queryFn: () => getApplication(id) });
}

export function useApplicationActivities(id: string) {
  return useQuery({ queryKey: activitiesKey(id), queryFn: () => getApplicationActivities(id) });
}

/* CV parse result. retry:false because a 404 ("not parsed yet") is an expected state, not a transient
   failure — retrying would just hammer the endpoint three times before showing the processing state. */
export function useCvParseResult(id: string) {
  return useQuery({ queryKey: cvParseKey(id), queryFn: () => getCvParseResult(id), retry: false });
}

/* Move-stage, reject and hire mutations for the detail page. All invalidate the detail, its
   timeline, and the application lists/board so every view reflects the change. */
export function useApplicationActions(id: string) {
  const queryClient = useQueryClient();
  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: applicationDetailKey(id) });
    queryClient.invalidateQueries({ queryKey: activitiesKey(id) });
    queryClient.invalidateQueries({ queryKey: ['applications'] });
  };

  const move = useMutation({
    mutationFn: (targetStageId: string) => moveApplicationStage(id, targetStageId),
    onSuccess: invalidate,
  });
  const correctStage = useMutation({
    mutationFn: ({ targetStageId, reason }: { targetStageId: string; reason: string }) =>
      correctApplicationStage(id, targetStageId, reason),
    onSuccess: invalidate,
  });
  const reject = useMutation({
    mutationFn: (reason: string) => rejectApplication(id, reason),
    onSuccess: invalidate,
  });
  const hire = useMutation({
    mutationFn: () => hireApplication(id),
    onSuccess: invalidate,
  });

  return { move, correctStage, reject, hire };
}
