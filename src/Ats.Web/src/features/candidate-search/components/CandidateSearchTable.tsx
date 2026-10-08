import { useTranslation } from 'react-i18next';
import { Skeleton, Table, THead, TBody, TR, TH, TD } from '@/components/ui';
import type { CandidateSearchResult } from '../candidateSearchApi';

interface CandidateSearchTableProps {
  candidates: CandidateSearchResult[];
  /** Opens the candidate's applications. */
  onSelect: (candidate: CandidateSearchResult) => void;
}

function ColumnHeaders() {
  const { t } = useTranslation();
  return (
    <TR>
      <TH>{t('candidateSearch.col.name')}</TH>
      <TH>{t('candidateSearch.col.email')}</TH>
      <TH>{t('candidateSearch.col.phone')}</TH>
      <TH>{t('candidateSearch.col.links')}</TH>
    </TR>
  );
}

export function CandidateSearchTable({ candidates, onSelect }: CandidateSearchTableProps) {
  const { t } = useTranslation();

  return (
    <Table>
      <THead>
        <ColumnHeaders />
      </THead>
      <TBody>
        {candidates.map((candidate) => (
          <TR
            key={candidate.id}
            interactive
            onClick={() => onSelect(candidate)}
            className="cursor-pointer"
          >
            <TD className="font-medium">
              {candidate.firstName} {candidate.lastName}
            </TD>
            <TD className="text-text-muted">{candidate.email}</TD>
            <TD className="whitespace-nowrap text-text-muted">{candidate.phone ?? '—'}</TD>
            <TD>
              {candidate.linkedInUrl ? (
                /* stopPropagation so opening the profile does not also trigger the row's navigation.
                   noreferrer alongside _blank: without it the opened tab can reach back through
                   window.opener, and this URL is candidate-supplied. */
                <a
                  href={candidate.linkedInUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  onClick={(event) => event.stopPropagation()}
                  className="text-sm text-accent hover:underline"
                >
                  {t('candidateSearch.linkedIn')}
                </a>
              ) : (
                <span className="text-text-muted">—</span>
              )}
            </TD>
          </TR>
        ))}
      </TBody>
    </Table>
  );
}

export function CandidateSearchTableSkeleton() {
  return (
    <Table>
      <THead>
        <ColumnHeaders />
      </THead>
      <TBody>
        {Array.from({ length: 6 }).map((_, rowIndex) => (
          <TR key={rowIndex} interactive>
            {Array.from({ length: 4 }).map((__, cellIndex) => (
              <TD key={cellIndex}>
                <Skeleton className="h-4 w-full max-w-32" />
              </TD>
            ))}
          </TR>
        ))}
      </TBody>
    </Table>
  );
}
