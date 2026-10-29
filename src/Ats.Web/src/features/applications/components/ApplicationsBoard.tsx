import { useMemo, useState } from 'react';
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core';
import { useTranslation } from 'react-i18next';
import { KanbanCard, KanbanColumn } from '@/components/ui';
import { cn } from '@/lib/cn';
import { stageLabel } from '@/lib/stageLabel';
import type { ApplicationListItem, PipelineStage } from '@/types/application';

interface ApplicationsBoardProps {
  stages: PipelineStage[];
  applications: ApplicationListItem[];
  canManage: boolean;
  onMove: (applicationId: string, targetStageId: string) => void;
  /* The move waits until an interview is actually scheduled (see ApplicationsPage). */
  onScheduleInterview: (applicationId: string, candidateName: string) => void;
  onSelect: (id: string) => void;
}

function CardBody({ application }: { application: ApplicationListItem }) {
  return (
    <>
      <span className="font-medium text-text">{application.candidateName}</span>
      <span className="text-xs text-text-muted">{application.candidateEmail}</span>
    </>
  );
}

function BoardCard({
  application,
  canManage,
  onSelect,
}: {
  application: ApplicationListItem;
  canManage: boolean;
  onSelect: (id: string) => void;
}) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: application.id,
    disabled: !canManage,
  });

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      // A drag needs 6px of movement, so a plain click opens the detail.
      onClick={() => onSelect(application.id)}
      className={cn(
        'cursor-pointer',
        canManage && 'active:cursor-grabbing',
        isDragging && 'opacity-40',
      )}
    >
      <KanbanCard>
        <CardBody application={application} />
      </KanbanCard>
    </div>
  );
}

function BoardColumn({
  stage,
  applications,
  canManage,
  isValidDropTarget,
  onSelect,
}: {
  stage: PipelineStage;
  applications: ApplicationListItem[];
  canManage: boolean;
  /* Moves are forward only, so earlier columns don't accept the drop. */
  isValidDropTarget: boolean;
  onSelect: (id: string) => void;
}) {
  const { setNodeRef, isOver } = useDroppable({ id: stage.id, disabled: !isValidDropTarget });
  const { t } = useTranslation();
  return (
    <div
      ref={setNodeRef}
      className={cn(
        'rounded-2xl transition-opacity',
        isOver && 'ring-2 ring-accent',
        !isValidDropTarget && 'opacity-50',
      )}
    >
      <KanbanColumn title={stageLabel(stage.name, t)} count={applications.length}>
        {applications.map((application) => (
          <BoardCard
            key={application.id}
            application={application}
            canManage={canManage}
            onSelect={onSelect}
          />
        ))}
      </KanbanColumn>
    </div>
  );
}

/* Only cross-column moves matter (no order inside a column), so @dnd-kit/core is enough; no sortable. */
export function ApplicationsBoard({
  stages,
  applications,
  canManage,
  onMove,
  onScheduleInterview,
  onSelect,
}: ApplicationsBoardProps) {
  const [activeId, setActiveId] = useState<string | null>(null);
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));

  /* Hired/Rejected columns aren't shown: the board has only Active applications and those stages are
     only reachable through hire/reject. */
  const workingStages = useMemo(
    () => stages.filter((stage) => stage.type !== 'FinalHired' && stage.type !== 'FinalRejected'),
    [stages],
  );

  const applicationsByStage = useMemo(() => {
    const grouped = new Map<string, ApplicationListItem[]>(
      workingStages.map((stage) => [stage.id, []]),
    );
    for (const application of applications) {
      grouped.get(application.stageId)?.push(application);
    }
    return grouped;
  }, [workingStages, applications]);

  const activeApplication = applications.find((application) => application.id === activeId) ?? null;
  const activeStageOrder = activeApplication
    ? (workingStages.find((stage) => stage.id === activeApplication.stageId)?.order ?? null)
    : null;

  const handleDragStart = (event: DragStartEvent) => setActiveId(String(event.active.id));

  const handleDragEnd = (event: DragEndEvent) => {
    setActiveId(null);
    const targetStageId = event.over ? String(event.over.id) : null;
    if (!targetStageId) return;
    const application = applications.find((item) => item.id === String(event.active.id));
    if (!application || application.stageId === targetStageId) return;

    const targetStage = stages.find((stage) => stage.id === targetStageId);
    // The Interview column opens the scheduling dialog instead of moving the card.
    if (targetStage?.type === 'Interview') {
      onScheduleInterview(application.id, application.candidateName);
      return;
    }

    onMove(application.id, targetStageId);
  };

  return (
    <DndContext
      sensors={sensors}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragCancel={() => setActiveId(null)}
    >
      <div className="flex gap-4 overflow-x-auto pb-2">
        {workingStages.map((stage) => (
          <BoardColumn
            key={stage.id}
            stage={stage}
            applications={applicationsByStage.get(stage.id) ?? []}
            canManage={canManage}
            isValidDropTarget={activeStageOrder === null || stage.order > activeStageOrder}
            onSelect={onSelect}
          />
        ))}
      </div>
      <DragOverlay>
        {activeApplication ? (
          <KanbanCard className="cursor-grabbing">
            <CardBody application={activeApplication} />
          </KanbanCard>
        ) : null}
      </DragOverlay>
    </DndContext>
  );
}
