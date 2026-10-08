import { describe, expect, it } from 'vitest';
import type {
  CandidateApplicationDetail,
  CandidatePipelineStage,
  CandidateTimelineEntry,
} from './candidateApplicationsApi';
import { buildTrackingSteps, isHiredStage } from './trackingSteps';

/* The candidate's tracking page is a dumb renderer over this function, so every rule the page
   appears to have actually lives here — and each one exists because the naive version read badly to
   a candidate: a stage announced twice, a roadmap shown to someone who withdrew, "Rejected" listed
   ahead of an active application as if it were coming.
 */

const stages: CandidatePipelineStage[] = [
  { id: 'applied', name: 'Applied', type: 'Initial', order: 1 },
  { id: 'screening', name: 'Screening', type: 'Active', order: 2 },
  { id: 'interview', name: 'Interview', type: 'Interview', order: 3 },
  { id: 'offer', name: 'Offer', type: 'Active', order: 4 },
  { id: 'hired', name: 'Hired', type: 'FinalHired', order: 5 },
  { id: 'rejected', name: 'Rejected', type: 'FinalRejected', order: 6 },
];

function detailOf(overrides: Partial<CandidateApplicationDetail>): CandidateApplicationDetail {
  return {
    id: 'application-1',
    jobTitle: 'Staff Engineer',
    jobSlug: 'staff-engineer',
    companyName: 'Acme',
    companySlug: 'acme',
    status: 'Active',
    appliedAtUtc: '2026-07-01T09:00:00Z',
    firstViewedAtUtc: null,
    currentStageId: 'applied',
    pipelineStages: stages,
    timeline: [],
    interviews: [],
    ...overrides,
  };
}

const submitted: CandidateTimelineEntry = {
  type: 'Submitted',
  stageName: null,
  occurredAtUtc: '2026-07-01T09:00:00Z',
};

const movedToScreening: CandidateTimelineEntry = {
  type: 'StageChanged',
  stageName: 'Screening',
  occurredAtUtc: '2026-07-05T10:00:00Z',
};

const movedToInterview: CandidateTimelineEntry = {
  type: 'StageChanged',
  stageName: 'Interview',
  occurredAtUtc: '2026-07-09T11:00:00Z',
};

describe('buildTrackingSteps — the current stage', () => {
  it('folds the move that produced the current stage into one dated step', () => {
    // Otherwise the same moment reads twice: "moved to Screening" on the 5th, then an undated
    // "Screening, in review" directly under it.
    const steps = buildTrackingSteps(
      detailOf({
        currentStageId: 'screening',
        timeline: [submitted, movedToScreening],
      }),
    );

    expect(steps.filter((s) => s.label === 'movedTo')).toHaveLength(0);

    const current = steps.find((s) => s.isCurrent)!;
    expect(current.stageName).toBe('Screening');
    expect(current.occurredAtUtc).toBe(movedToScreening.occurredAtUtc);
  });

  it('dates the current stage from the application when no move produced it', () => {
    // An application still sitting in the initial stage has no StageChanged event to borrow a date
    // from, so the submission date is the honest answer rather than no date at all.
    const steps = buildTrackingSteps(detailOf({ currentStageId: 'applied', timeline: [submitted] }));

    const current = steps.find((s) => s.isCurrent)!;
    expect(current.stageName).toBe('Applied');
    expect(current.occurredAtUtc).toBe('2026-07-01T09:00:00Z');
  });

  it('uses the latest arrival when a stage was entered more than once', () => {
    // A recruiter corrected a move and sent the application back to Screening. The candidate got
    // here on the 12th, not the 5th; searching from the front would report the stale date.
    const secondArrival: CandidateTimelineEntry = {
      type: 'StageChanged',
      stageName: 'Screening',
      occurredAtUtc: '2026-07-12T08:00:00Z',
    };

    const steps = buildTrackingSteps(
      detailOf({
        currentStageId: 'screening',
        timeline: [submitted, movedToScreening, movedToInterview, secondArrival],
      }),
    );

    const current = steps.find((s) => s.isCurrent)!;
    expect(current.occurredAtUtc).toBe('2026-07-12T08:00:00Z');

    // The earlier arrival stays in the history as an ordinary event — only the latest is folded.
    expect(steps.filter((s) => s.label === 'movedTo')).toHaveLength(2);
  });
});

describe('buildTrackingSteps — the roadmap', () => {
  it('lists the stages still ahead, in funnel order', () => {
    const steps = buildTrackingSteps(
      detailOf({ currentStageId: 'screening', timeline: [submitted, movedToScreening] }),
    );

    expect(steps.filter((s) => s.label === 'upcoming').map((s) => s.stageName)).toEqual([
      'Interview',
      'Offer',
      'Hired',
    ]);
  });

  it('never puts the rejection stage on the roadmap', () => {
    // It is an exit, not a step. Listing it ahead of an active candidate reads as a threat.
    const steps = buildTrackingSteps(detailOf({ currentStageId: 'applied', timeline: [submitted] }));

    expect(steps.every((s) => s.stageName !== 'Rejected')).toBe(true);
  });

  it.each(['Rejected', 'Hired', 'Withdrawn'] as const)(
    'shows no roadmap once the application is %s',
    (status) => {
      // A terminal application's story is fully told by its events. Showing the stages still ahead
      // of someone who withdrew would read as a process they are still in.
      const steps = buildTrackingSteps(
        detailOf({
          status,
          currentStageId: 'screening',
          timeline: [submitted, movedToScreening],
        }),
      );

      expect(steps.filter((s) => s.label === 'upcoming')).toHaveLength(0);
      expect(steps.filter((s) => s.isCurrent)).toHaveLength(0);
      // ...and the move is no longer folded away, since there is no current step to fold it into.
      expect(steps.filter((s) => s.label === 'movedTo')).toHaveLength(1);
    },
  );

  it('gives the last stage no roadmap beyond itself', () => {
    const steps = buildTrackingSteps(
      detailOf({
        currentStageId: 'offer',
        timeline: [submitted, { ...movedToScreening, stageName: 'Offer' }],
      }),
    );

    expect(steps.filter((s) => s.label === 'upcoming').map((s) => s.stageName)).toEqual(['Hired']);
  });
});

describe('buildTrackingSteps — event mapping', () => {
  it('tones a withdrawal neutral rather than as a failure', () => {
    // The candidate chose to stop. Reporting it back to them in the same red as a rejection is a
    // small cruelty and a factual error.
    const steps = buildTrackingSteps(
      detailOf({
        status: 'Withdrawn',
        timeline: [submitted, { type: 'Withdrawn', stageName: null, occurredAtUtc: '2026-07-08T12:00:00Z' }],
      }),
    );

    expect(steps.find((s) => s.label === 'withdrawn')!.tone).toBe('neutral');
    expect(steps.find((s) => s.label === 'rejected')).toBeUndefined();
  });

  it('gives every step a unique key', () => {
    // React lists de-duplicate on key; a collision drops a step from the page silently.
    const steps = buildTrackingSteps(
      detailOf({
        currentStageId: 'interview',
        timeline: [submitted, movedToScreening, movedToInterview],
      }),
    );

    expect(new Set(steps.map((s) => s.key)).size).toBe(steps.length);
  });

  it('picks a distinct icon per stage instead of a column of identical circles', () => {
    const steps = buildTrackingSteps(detailOf({ currentStageId: 'applied', timeline: [submitted] }));
    const roadmapIcons = steps.filter((s) => s.isCurrent || s.label === 'upcoming').map((s) => s.kind);

    expect(new Set(roadmapIcons).size).toBe(roadmapIcons.length);
  });

  it('falls back to a generic icon for a stage name it does not recognise', () => {
    // Screening and Offer are both type Active, so the names are a display nicety. A tenant that
    // renames them must still get a usable icon rather than a wrong guess.
    const renamed: CandidatePipelineStage[] = [
      { id: 'applied', name: 'Applied', type: 'Initial', order: 1 },
      { id: 'custom', name: 'Take-home exercise', type: 'Active', order: 2 },
    ];

    const steps = buildTrackingSteps(
      detailOf({ currentStageId: 'custom', pipelineStages: renamed, timeline: [submitted] }),
    );

    expect(steps.find((s) => s.isCurrent)!.kind).toBe('stage');
  });
});

describe('buildTrackingSteps — degenerate input', () => {
  it('survives a current stage that is not in the pipeline', () => {
    // Should not happen, but the page must not blank out if it ever does: the history is still
    // worth showing even when the roadmap cannot be built.
    const steps = buildTrackingSteps(
      detailOf({ currentStageId: 'does-not-exist', timeline: [submitted] }),
    );

    expect(steps.filter((s) => s.isCurrent)).toHaveLength(0);
    expect(steps.filter((s) => s.label === 'submitted')).toHaveLength(1);
  });

  it('handles an empty timeline', () => {
    const steps = buildTrackingSteps(detailOf({ currentStageId: 'applied', timeline: [] }));

    expect(steps.find((s) => s.isCurrent)!.stageName).toBe('Applied');
  });
});

describe('isHiredStage', () => {
  it('recognises the pipeline stage typed FinalHired', () => {
    expect(isHiredStage('Hired', stages)).toBe(true);
  });

  it('answers on type, not on the name looking like a hire', () => {
    // A tenant may name any stage "Hired"; only the type decides whether the page celebrates.
    const misleading: CandidatePipelineStage[] = [
      { id: 'x', name: 'Hired', type: 'Active', order: 1 },
    ];

    expect(isHiredStage('Hired', misleading)).toBe(false);
  });

  it('is false for a null stage name', () => {
    expect(isHiredStage(null, stages)).toBe(false);
  });
});
