import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

const PATHS = {
  candidate: { login: '/candidate/login', register: '/candidate/register' },
  company: { login: '/login', register: '/register' },
} as const;

type Audience = keyof typeof PATHS;

/* Two-pill switch shown on every auth screen so a visitor who landed on the wrong door
   (job seeker vs employer) sees which one they are on and can cross over in one click.
   The active side is a plain span — linking a pill to the page it is already on would be
   dead navigation. Router state is passed through so a candidate keeps their post-login
   return target when switching between login and register. */
export function AudienceSwitch({
  active,
  variant,
  state,
}: {
  active: Audience;
  variant: 'login' | 'register';
  state?: unknown;
}) {
  const { t } = useTranslation();

  const pill = (audience: Audience, label: string) =>
    audience === active ? (
      <span
        aria-current="page"
        className="flex-1 rounded-md bg-accent px-3 py-1.5 text-center text-sm font-medium text-accent-fg"
      >
        {label}
      </span>
    ) : (
      <Link
        to={PATHS[audience][variant]}
        state={state}
        className="flex-1 rounded-md px-3 py-1.5 text-center text-sm text-text-muted transition-colors hover:text-text"
      >
        {label}
      </Link>
    );

  return (
    <nav
      aria-label={t('authSwitch.label')}
      className="flex rounded-lg border border-border bg-card p-1"
    >
      {pill('candidate', t('authSwitch.candidate'))}
      {pill('company', t('authSwitch.company'))}
    </nav>
  );
}
