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
import { KanbanCard, KanbanColumn } from '@/components/ui';
import { cn } from '@/lib/cn';
import type { ApplicationListItem, PipelineStage } from '@/types/application';

interface ApplicationsBoardProps {
  stages: PipelineStage[];
  applications: ApplicationListItem[];
  /** Read-only roles see the board but can't drag cards. */
  canManage: boolean;
  onMove: (applicationId: string, targetStageId: string) => void;
  /** Clicking a card (without dragging) opens its detail page. */
  onSelect: (id: string) => void;
}

/* The visual content of a card, reused by the column cards and the drag overlay. */
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
      // A drag needs 6px of movement to start, so a plain click falls through to open the detail.
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
  onSelect,
}: {
  stage: PipelineStage;
  applications: ApplicationListItem[];
  canManage: boolean;
  onSelect: (id: string) => void;
}) {
  const { setNodeRef, isOver } = useDroppable({ id: stage.id });
  return (
    <div ref={setNodeRef} className={cn('rounded-2xl', isOver && 'ring-2 ring-accent')}>
      <KanbanColumn title={stage.name} count={applications.length}>
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

/* Drag-and-drop pipeline board built on @dnd-kit/core: it gives us pointer + touch dragging, a
   floating DragOverlay, and droppable columns without hand-rolling the HTML5 drag API. Only
   cross-column moves are meaningful (there's no intra-column order to persist), so plain
   draggable/droppable suffices — no sortable. Cards are grouped by their current stage id. */
export function ApplicationsBoard({ stages, applications, canManage, onMove, onSelect }: ApplicationsBoardProps) {
  const [activeId, setActiveId] = useState<string | null>(null);
  // A small activation distance so a click on a card isn't mistaken for a drag.
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));

  const applicationsByStage = useMemo(() => {
    const grouped = new Map<string, ApplicationListItem[]>(stages.map((stage) => [stage.id, []]));
    for (const application of applications) {
      grouped.get(application.stageId)?.push(application);
    }
    return grouped;
  }, [stages, applications]);

  const activeApplication = applications.find((application) => application.id === activeId) ?? null;

  const handleDragStart = (event: DragStartEvent) => setActiveId(String(event.active.id));

  const handleDragEnd = (event: DragEndEvent) => {
    setActiveId(null);
    const targetStageId = event.over ? String(event.over.id) : null;
    if (!targetStageId) return;
    const application = applications.find((item) => item.id === String(event.active.id));
    if (application && application.stageId !== targetStageId) {
      onMove(application.id, targetStageId);
    }
  };

  return (
    <DndContext
      sensors={sensors}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragCancel={() => setActiveId(null)}
    >
      <div className="flex gap-4 overflow-x-auto pb-2">
        {stages.map((stage) => (
          <BoardColumn
            key={stage.id}
            stage={stage}
            applications={applicationsByStage.get(stage.id) ?? []}
            canManage={canManage}
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
