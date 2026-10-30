import { useTranslation } from 'react-i18next';

/* The careers page lives outside the app shell, so this is a plain anchor with target=_blank, not
   a router <Link>. */
export function CareersPageLink({ slug }: { slug: string }) {
  const { t } = useTranslation();
  return (
    <a
      href={`/${slug}`}
      target="_blank"
      rel="noreferrer"
      className="inline-flex items-center gap-1.5 text-sm font-medium text-accent hover:underline"
    >
      {t('common.viewCareers')}
      <ExternalLinkIcon />
    </a>
  );
}

function ExternalLinkIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M15 3h6v6M10 14 21 3M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
    </svg>
  );
}
