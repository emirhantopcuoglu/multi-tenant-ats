import { cn, focusRing } from '@/lib/cn';

interface PaginationProps {
  /** Current page, 1-based. */
  page: number;
  pageCount: number;
  onPageChange: (page: number) => void;
}

type PageItem = number | 'ellipsis';

/* Build a compact page list: always the first and last page, a window around the current page, and
   ellipses for the gaps. Keeps the control short even with hundreds of pages. */
function getPageItems(page: number, pageCount: number): PageItem[] {
  const SIBLINGS = 1;
  const pages = new Set<number>([1, pageCount]);
  for (let p = page - SIBLINGS; p <= page + SIBLINGS; p++) {
    if (p >= 1 && p <= pageCount) pages.add(p);
  }

  const sorted = [...pages].sort((a, b) => a - b);
  const items: PageItem[] = [];
  let previous = 0;
  for (const current of sorted) {
    if (current - previous > 1) items.push('ellipsis');
    items.push(current);
    previous = current;
  }
  return items;
}

const arrowClasses =
  'flex h-8 w-8 items-center justify-center rounded-lg border border-border bg-card text-text transition-colors hover:bg-divider disabled:cursor-not-allowed disabled:text-text-disabled disabled:hover:bg-card';

export function Pagination({ page, pageCount, onPageChange }: PaginationProps) {
  if (pageCount <= 1) return null;

  return (
    <nav aria-label="Pagination" className="flex items-center gap-1.5">
      <button
        type="button"
        aria-label="Previous page"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
        className={cn(arrowClasses, focusRing)}
      >
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <polyline points="15 18 9 12 15 6" />
        </svg>
      </button>

      {getPageItems(page, pageCount).map((item, index) =>
        item === 'ellipsis' ? (
          <span key={`ellipsis-${index}`} className="px-1 text-sm text-text-muted">
            …
          </span>
        ) : (
          <button
            key={item}
            type="button"
            aria-current={item === page ? 'page' : undefined}
            onClick={() => onPageChange(item)}
            className={cn(
              'h-8 min-w-8 rounded-lg border px-2 text-sm transition-colors',
              item === page
                ? 'border-accent bg-accent text-accent-fg'
                : 'border-border bg-card text-text hover:bg-divider',
              focusRing,
            )}
          >
            {item}
          </button>
        ),
      )}

      <button
        type="button"
        aria-label="Next page"
        disabled={page >= pageCount}
        onClick={() => onPageChange(page + 1)}
        className={cn(arrowClasses, focusRing)}
      >
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <polyline points="9 18 15 12 9 6" />
        </svg>
      </button>
    </nav>
  );
}
