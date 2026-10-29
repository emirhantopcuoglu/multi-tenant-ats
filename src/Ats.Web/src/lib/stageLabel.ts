import type { TFunction } from 'i18next';

/* Translates the default stage names; anything else (e.g. a custom stage) shows as is. */
const KNOWN_STAGE_NAMES = ['Applied', 'Screening', 'Interview', 'Offer', 'Hired', 'Rejected'] as const;
type KnownStageName = (typeof KNOWN_STAGE_NAMES)[number];

function isKnownStageName(name: string): name is KnownStageName {
  return (KNOWN_STAGE_NAMES as readonly string[]).includes(name);
}

export function stageLabel(name: string, t: TFunction): string {
  // Must stay a narrow literal union, or the typed t() overloads hit "excessively deep" type errors.
  return isKnownStageName(name) ? t(`stage.${name}`) : name;
}
