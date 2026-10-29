import { Fragment, type ReactNode } from 'react';

export interface BreadcrumbItem {
  label: ReactNode;
  /* Leave href off the current item. */
  href?: string;
}

/* The consumer handles navigation through onNavigate. */
export function Breadcrumb({
  items,
  onNavigate,
}: {
  items: BreadcrumbItem[];
  onNavigate?: (href: string) => void;
}) {
  return (
    <nav aria-label="Breadcrumb">
      <ol className="flex items-center gap-2 text-sm text-text-muted">
        {items.map((item, index) => {
          const isLast = index === items.length - 1;
          return (
            <Fragment key={index}>
              <li>
                {item.href && !isLast ? (
                  <button
                    type="button"
                    onClick={() => onNavigate?.(item.href!)}
                    className="transition-colors hover:text-text focus-visible:outline-none focus-visible:text-text"
                  >
                    {item.label}
                  </button>
                ) : (
                  <span aria-current={isLast ? 'page' : undefined} className={isLast ? 'font-medium text-text' : undefined}>
                    {item.label}
                  </span>
                )}
              </li>
              {!isLast && (
                <li aria-hidden="true" className="text-text-disabled">
                  /
                </li>
              )}
            </Fragment>
          );
        })}
      </ol>
    </nav>
  );
}
