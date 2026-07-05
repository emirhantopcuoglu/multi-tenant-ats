import type { useTranslation } from 'react-i18next';

/* Pipeline stage names come from the backend as plain, English strings (the MVP's only pipeline
   template — see Pipeline.CreateDefault). The `stage.*` i18n keys already exist and are already
   translated in both locales; they were simply never wired into any of the screens that render a
   stage name, which is why the UI showed English stage names even in Turkish. This is the single
   place that wiring happens. Falls back to the raw name for anything the keys don't cover — a
   future custom-pipeline stage a company names themselves has no translation and shouldn't show
   a missing-key artifact. */
const KNOWN_STAGE_NAMES = ['Applied', 'Screening', 'Interview', 'Offer', 'Hired', 'Rejected'] as const;
type KnownStageName = (typeof KNOWN_STAGE_NAMES)[number];

function isKnownStageName(name: string): name is KnownStageName {
  return (KNOWN_STAGE_NAMES as readonly string[]).includes(name);
}

export function stageLabel(name: string, t: ReturnType<typeof useTranslation>['t']): string {
  // The key must stay a narrow literal union (KnownStageName), not a general string, or
  // react-i18next's typed `t` overload resolution blows up (excessively deep type instantiation) —
  // the same reason other call sites in this app only ever interpolate an already-typed enum value.
  return isKnownStageName(name) ? t(`stage.${name}`) : name;
}
