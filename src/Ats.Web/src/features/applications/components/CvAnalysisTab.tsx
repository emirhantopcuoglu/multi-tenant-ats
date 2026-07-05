import { useTranslation } from 'react-i18next';
import { Badge, Skeleton } from '@/components/ui';
import { toApiError } from '@/lib/problemDetails';
import { cvJobFitRatingTone } from '@/lib/statusColors';
import { useCvParseResult } from '../useApplicationDetail';

/* The async CV-parse result. A 404 with code "application.cv_not_parsed" is the expected "still
   processing" state (parsing runs in the background), distinguished from a real load error. */
export function CvAnalysisTab({ applicationId }: { applicationId: string }) {
  const { t } = useTranslation();
  const { data, isLoading, isError, error } = useCvParseResult(applicationId);

  if (isLoading) return <Skeleton className="h-40 w-full" />;

  if (isError) {
    const notParsed = toApiError(error).code === 'application.cv_not_parsed';
    return (
      <p className="text-sm text-text-muted">
        {t(notParsed ? 'applicationDetail.analysis.processing' : 'applicationDetail.analysis.error')}
      </p>
    );
  }

  if (!data) return <p className="text-sm text-text-muted">{t('applicationDetail.analysis.empty')}</p>;

  return (
    <div className="space-y-5 text-sm">
      <section className="space-y-2 rounded-lg border border-border p-4">
        <div className="flex items-center justify-between gap-2">
          <h3 className="font-semibold text-text">{t('applicationDetail.analysis.fitTitle')}</h3>
          <Badge tone={cvJobFitRatingTone[data.jobFitRating]}>
            {t(`applicationDetail.analysis.fitRating.${data.jobFitRating}`)}
          </Badge>
        </div>
        {data.fitSummary && <p className="text-text-muted">{data.fitSummary}</p>}

        {data.matchedRequirements.length > 0 && (
          <div className="space-y-1.5 pt-1">
            <h4 className="text-xs font-medium text-text-muted">
              {t('applicationDetail.analysis.matchedRequirements')}
            </h4>
            <div className="flex flex-wrap gap-1.5">
              {data.matchedRequirements.map((item) => (
                <Badge key={item} tone="success">
                  {item}
                </Badge>
              ))}
            </div>
          </div>
        )}

        {data.missingRequirements.length > 0 && (
          <div className="space-y-1.5 pt-1">
            <h4 className="text-xs font-medium text-text-muted">
              {t('applicationDetail.analysis.missingRequirements')}
            </h4>
            <div className="flex flex-wrap gap-1.5">
              {data.missingRequirements.map((item) => (
                <Badge key={item} tone="gray">
                  {item}
                </Badge>
              ))}
            </div>
          </div>
        )}
      </section>

      <section className="space-y-2">
        <h3 className="font-semibold text-text">{t('applicationDetail.analysis.experience')}</h3>
        <p className="text-text-muted">
          {t('applicationDetail.analysis.years', { count: data.totalExperienceYears })}
        </p>
      </section>

      {data.skills.length > 0 && (
        <section className="space-y-2">
          <h3 className="font-semibold text-text">{t('applicationDetail.analysis.skills')}</h3>
          <div className="flex flex-wrap gap-1.5">
            {data.skills.map((skill) => (
              <Badge key={skill} tone="accent">
                {skill}
              </Badge>
            ))}
          </div>
        </section>
      )}

      {data.recentPositions.length > 0 && (
        <section className="space-y-2">
          <h3 className="font-semibold text-text">{t('applicationDetail.analysis.positions')}</h3>
          <ul className="space-y-1.5">
            {data.recentPositions.map((position, index) => (
              <li key={index} className="text-text-muted">
                <span className="text-text">{position.title}</span> · {position.company}{' '}
                <span className="text-xs">
                  ({position.startDate} – {position.endDate})
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {data.education.length > 0 && (
        <section className="space-y-2">
          <h3 className="font-semibold text-text">{t('applicationDetail.analysis.education')}</h3>
          <ul className="space-y-1.5">
            {data.education.map((entry, index) => (
              <li key={index} className="text-text-muted">
                <span className="text-text">{entry.degree}</span> · {entry.institution}{' '}
                <span className="text-xs">({entry.year})</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
