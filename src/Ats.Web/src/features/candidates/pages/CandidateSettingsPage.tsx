import type { ReactNode } from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ThemeToggle } from '@/components/ThemeToggle';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';
import { SidebarNavItem } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';

/* Same stroke language as the company nav's icons (navConfig.tsx), kept local rather than shared —
   this rail belongs to the candidate settings page only, not the authenticated app shell. */
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

const SETTINGS_NAV_ITEMS = [
  {
    path: '/candidate/settings/profile',
    labelKey: 'candidateSettings.nav.profile' as const,
    icon: (
      <Glyph>
        <circle cx="12" cy="8" r="4" />
        <path d="M4 21v-1a6 6 0 0 1 6-6h4a6 6 0 0 1 6 6v1" />
      </Glyph>
    ),
  },
  {
    path: '/candidate/settings/security',
    labelKey: 'candidateSettings.nav.security' as const,
    icon: (
      <Glyph>
        <rect x="4" y="11" width="16" height="9" rx="2" />
        <path d="M8 11V7a4 4 0 0 1 8 0v4" />
      </Glyph>
    ),
  },
  {
    path: '/candidate/settings/account',
    labelKey: 'candidateSettings.nav.account' as const,
    icon: (
      <Glyph>
        <circle cx="12" cy="12" r="9" />
        <path d="M12 8v5M12 16h.01" />
      </Glyph>
    ),
  },
];

/* Thin chrome for the candidate settings area: PublicLayout's marketing header/footer would just be
   dead weight here, so this is its own minimal shell — brand mark, theme/language toggles, sign out
   — plus a left-hand section nav next to the routed tab (<Outlet/>). Each tab is a real route
   (/candidate/settings/profile|security|account) so it is bookmarkable and gets its own history
   entry, rather than being client-only tab state. */
export function CandidateSettingsPage() {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const { logout } = useAuth();

  return (
    <div className="flex min-h-screen flex-col bg-bg text-text">
      <header className="border-b border-border">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
          <Link to="/" className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-accent text-sm font-bold text-accent-fg">
              A
            </span>
            <span className="font-semibold">{t('common.appName')}</span>
          </Link>
          <div className="flex items-center gap-3">
            <LanguageSwitcher />
            <ThemeToggle />
            <button
              type="button"
              onClick={() => void logout()}
              className="text-sm text-text-muted hover:text-text"
            >
              {t('common.signOut')}
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">
        <div className="mb-6 space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">{t('candidateSettings.title')}</h1>
          <p className="text-sm text-text-muted">{t('candidateSettings.subtitle')}</p>
        </div>

        <div className="flex flex-col gap-8 lg:flex-row">
          <nav className="flex gap-1 overflow-x-auto lg:w-56 lg:shrink-0 lg:flex-col lg:overflow-visible">
            {SETTINGS_NAV_ITEMS.map((item) => (
              <SidebarNavItem
                key={item.path}
                icon={item.icon}
                label={t(item.labelKey)}
                active={pathname.startsWith(item.path)}
                onClick={() => navigate(item.path)}
              />
            ))}
          </nav>

          <div className="min-w-0 flex-1">
            <Outlet />
          </div>
        </div>
      </main>
    </div>
  );
}
