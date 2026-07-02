import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useToast } from '@/components/ui';
import type { PagedResult } from '@/types/pagination';
import type { ApplicationListItem } from '@/types/application';
import { listApplications, moveApplicationStage } from './applicationsApi';
import { useJobStages } from './useApplications';

const BOARD_PAGE_SIZE = 100;
const boardKey = (jobId: string) => ['applications', 'board', jobId] as const;

/* Data + move mutation for the Kanban board of one job. The board shows only Active applications
   (terminal ones can't be moved), grouped client-side by stage. The move is optimistic: the card
   jumps to the target column immediately, and the previous board is restored if the request fails. */
export function useApplicationsBoard(jobId: string | undefined) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { t } = useTranslation();

  const applicationsQuery = useQuery({
    queryKey: jobId ? boardKey(jobId) : ['applications', 'board', 'none'],
    queryFn: () => listApplications({ page: 1, pageSize: BOARD_PAGE_SIZE, jobId, status: 'Active' }),
    enabled: Boolean(jobId),
  });

  const stagesQuery = useJobStages(jobId);

  const move = useMutation({
    mutationFn: ({ id, targetStageId }: { id: string; targetStageId: string }) =>
      moveApplicationStage(id, targetStageId),
    onMutate: async ({ id, targetStageId }) => {
      if (!jobId) return undefined;
      const key = boardKey(jobId);
      // Cancel in-flight refetches so they don't clobber the optimistic state.
      await queryClient.cancelQueries({ queryKey: key });
      const previous = queryClient.getQueryData<PagedResult<ApplicationListItem>>(key);
      const stageName = stagesQuery.data?.find((stage) => stage.id === targetStageId)?.name ?? '';
      queryClient.setQueryData<PagedResult<ApplicationListItem>>(key, (old) =>
        old
          ? {
              ...old,
              items: old.items.map((item) =>
                item.id === id ? { ...item, stageId: targetStageId, stageName } : item,
              ),
            }
          : old,
      );
      return { key, previous };
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(context.key, context.previous);
      toast({ title: t('applications.moveError'), tone: 'danger' });
    },
    onSettled: () => {
      if (jobId) queryClient.invalidateQueries({ queryKey: boardKey(jobId) });
    },
  });

  return { applicationsQuery, stagesQuery, move };
}
