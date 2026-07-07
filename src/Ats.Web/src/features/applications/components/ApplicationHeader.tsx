import { useTranslation } from 'react-i18next';
import { Avatar, Badge, Button, Card, Dropdown, type DropdownAction } from '@/components/ui';
import { applicationStatusTone } from '@/lib/statusColors';
import { stageLabel } from '@/lib/stageLabel';
import type { ApplicationDetail, PipelineStage } from '@/types/application';

interface ApplicationHeaderProps {
  application: ApplicationDetail;
  stages: PipelineStage[];
  canManage: boolean;
  onMove: (stageId: string) => void;
  onHireClick: () => void;
  onRejectClick: () => void;
  busy: boolean;
}

/* Detail header: candidate identity, current stage + status, and (for managers, while the
   application is still Active) the move-stage menu and the two terminal decisions. */
export function ApplicationHeader({
  application,
  stages,
  canManage,
  onMove,
  onHireClick,
  onRejectClick,
  busy,
}: ApplicationHeaderProps) {
  const { t } = useTranslation();
  const isActive = application.status === 'Active';

  /* Terminal stages are outcomes, not move targets: reaching them must flip the application's
     status, which only the hire/reject actions do (the backend refuses them here too). */
  const moveItems: DropdownAction[] = stages
    .filter(
      (stage) =>
        stage.id !== application.stageId &&
        stage.type !== 'FinalHired' &&
        stage.type !== 'FinalRejected',
    )
    .map((stage) => ({ key: stage.id, label: stageLabel(stage.name, t), onSelect: () => onMove(stage.id) }));

  return (
    <Card className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex gap-4">
        <Avatar name={application.candidateName} size="lg" />
        <div className="space-y-1">
          <h2 className="text-lg font-semibold text-text">{application.candidateName}</h2>
          <p className="text-sm text-text-muted">{application.candidateEmail}</p>
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-text-muted">
            {application.phone && <span>{application.phone}</span>}
            {application.linkedInUrl && (
              <a
                href={application.linkedInUrl}
                target="_blank"
                rel="noreferrer"
                className="text-accent hover:underline"
              >
                {t('applicationDetail.linkedin')}
              </a>
            )}
          </div>
          <div className="flex items-center gap-2 pt-1">
            <Badge tone="neutral">{stageLabel(application.stageName, t)}</Badge>
            <Badge tone={applicationStatusTone[application.status]} dot>
              {t(`status.${application.status}`)}
            </Badge>
          </div>
        </div>
      </div>

      {canManage && isActive && (
        <div className="flex shrink-0 gap-2">
          {moveItems.length > 0 && (
            <Dropdown
              items={moveItems}
              trigger={
                <Button variant="secondary" disabled={busy}>
                  {t('applicationDetail.moveStage')}
                </Button>
              }
            />
          )}
          <Button variant="primary" onClick={onHireClick} disabled={busy}>
            {t('applicationDetail.hire')}
          </Button>
          <Button variant="danger" onClick={onRejectClick} disabled={busy}>
            {t('applicationDetail.reject')}
          </Button>
        </div>
      )}
    </Card>
  );
}
