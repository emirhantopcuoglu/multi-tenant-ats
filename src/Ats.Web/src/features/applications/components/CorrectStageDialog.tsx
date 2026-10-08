import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Modal, Select, Textarea } from '@/components/ui';
import { stageLabel } from '@/lib/stageLabel';
import type { PipelineStage } from '@/types/application';

interface CorrectStageDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Every non-terminal stage except the one the application is currently in. */
  stageOptions: PipelineStage[];
  onConfirm: (targetStageId: string, reason: string) => void;
  submitting: boolean;
}

/* The escape hatch for a wrong move-stage: unlike RejectDialog's single reason field, this also
   needs a target stage picker, since a correction can go in any direction. Both fields are
   required — a stage pick with no reason would leave the audit trail unable to explain why an
   application moved backward. */
export function CorrectStageDialog({
  open,
  onOpenChange,
  stageOptions,
  onConfirm,
  submitting,
}: CorrectStageDialogProps) {
  const { t } = useTranslation();
  const [targetStageId, setTargetStageId] = useState('');
  const [reason, setReason] = useState('');
  const [stageError, setStageError] = useState(false);
  const [reasonError, setReasonError] = useState(false);

  // Reset when the dialog opens, so a previous draft never lingers. Adjusted during render rather
  // than in an effect: React discards the in-progress output and re-renders before committing, so
  // the cleared form is what reaches the DOM. An effect would paint the stale draft first and clear
  // it afterwards, which is the cascading render the linter flags.
  const [prevOpen, setPrevOpen] = useState(open);
  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) {
      setTargetStageId('');
      setReason('');
      setStageError(false);
      setReasonError(false);
    }
  }

  const handleConfirm = () => {
    const hasStage = Boolean(targetStageId);
    const hasReason = Boolean(reason.trim());
    setStageError(!hasStage);
    setReasonError(!hasReason);
    if (!hasStage || !hasReason) return;
    onConfirm(targetStageId, reason.trim());
  };

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('applicationDetail.correct_stage_modal.title')}
      description={t('applicationDetail.correct_stage_modal.description')}
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="secondary" onClick={handleConfirm} disabled={submitting}>
            {t('applicationDetail.correct_stage_modal.confirm')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="space-y-1.5">
          <label htmlFor="correct-stage-target" className="block text-sm font-medium text-text">
            {t('applicationDetail.correct_stage_modal.stageLabel')}
          </label>
          <Select
            id="correct-stage-target"
            value={targetStageId}
            onChange={(event) => {
              setTargetStageId(event.target.value);
              if (stageError) setStageError(false);
            }}
            invalid={stageError}
          >
            <option value="">{t('applicationDetail.correct_stage_modal.stagePlaceholder')}</option>
            {stageOptions.map((stage) => (
              <option key={stage.id} value={stage.id}>
                {stageLabel(stage.name, t)}
              </option>
            ))}
          </Select>
          {stageError && (
            <p className="text-xs text-danger">{t('applicationDetail.correct_stage_modal.stageRequired')}</p>
          )}
        </div>

        <div className="space-y-1.5">
          <label htmlFor="correct-stage-reason" className="block text-sm font-medium text-text">
            {t('applicationDetail.correct_stage_modal.reasonLabel')}
          </label>
          <Textarea
            id="correct-stage-reason"
            value={reason}
            onChange={(event) => {
              setReason(event.target.value);
              if (reasonError) setReasonError(false);
            }}
            invalid={reasonError}
            placeholder={t('applicationDetail.correct_stage_modal.reasonPlaceholder')}
            rows={3}
          />
          {reasonError && (
            <p className="text-xs text-danger">{t('applicationDetail.correct_stage_modal.reasonRequired')}</p>
          )}
        </div>
      </div>
    </Modal>
  );
}
