import type { ReactNode } from 'react';
import type { Role } from '@/types/enums';

/* Restricting labelKey to the actual `nav.*` keys keeps it assignable to the type-safe `t()` (a bare
   `string` is not). Adding a nav item therefore forces both a real translation and a real icon. */
export type NavLabelKey =
  | 'nav.overview'
  | 'nav.jobs'
  | 'nav.applications'
  | 'nav.interviews'
  | 'nav.candidates'
  | 'nav.settings';

export interface NavItem {
  path: string;
  labelKey: NavLabelKey;
  icon: ReactNode;
  /** When present, only these roles see the item. Undefined = visible to every authenticated user. */
  roles?: Role[];
}

/* Shared SVG wrapper so each glyph only carries its own paths (same stroke language as ThemeToggle). */
function Glyph({ children }: { children: ReactNode }) {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {children}
    </svg>
  );
}

/* Single source of truth for navigation, consumed by both the Sidebar (renders the items) and the
   Topbar (derives the page title from the active path). Order here is the order shown in the rail. */
export const NAV_ITEMS: NavItem[] = [
  {
    path: '/',
    labelKey: 'nav.overview',
    icon: (
      <Glyph>
        <rect x="3" y="3" width="7" height="7" rx="1" />
        <rect x="14" y="3" width="7" height="7" rx="1" />
        <rect x="14" y="14" width="7" height="7" rx="1" />
        <rect x="3" y="14" width="7" height="7" rx="1" />
      </Glyph>
    ),
  },
  {
    path: '/jobs',
    labelKey: 'nav.jobs',
    icon: (
      <Glyph>
        <rect x="2" y="7" width="20" height="14" rx="2" />
        <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
      </Glyph>
    ),
  },
  {
    path: '/applications',
    labelKey: 'nav.applications',
    icon: (
      <Glyph>
        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
        <path d="M14 2v6h6" />
        <path d="M8 13h8M8 17h6" />
      </Glyph>
    ),
  },
  {
    path: '/interviews',
    labelKey: 'nav.interviews',
    icon: (
      <Glyph>
        <rect x="3" y="4" width="18" height="18" rx="2" />
        <path d="M16 2v4M8 2v4M3 10h18" />
      </Glyph>
    ),
  },
  {
    path: '/candidates',
    labelKey: 'nav.candidates',
    icon: (
      <Glyph>
        <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
        <circle cx="9" cy="7" r="4" />
        <path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
      </Glyph>
    ),
  },
  {
    path: '/settings',
    labelKey: 'nav.settings',
    roles: ['Admin'],
    icon: (
      <Glyph>
        <circle cx="12" cy="12" r="3" />
        <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
      </Glyph>
    ),
  },
];

/* The overview lives at the root, so it must match exactly; every other item also matches its own
   sub-paths (e.g. /jobs/42 keeps "Jobs" highlighted) without bleeding into siblings. */
export function isNavItemActive(pathname: string, path: string): boolean {
  if (path === '/') return pathname === '/';
  return pathname === path || pathname.startsWith(`${path}/`);
}
