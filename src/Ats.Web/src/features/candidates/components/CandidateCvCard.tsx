import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, useToast } from '@/components/ui';
import { CvDropZone } from '@/features/public/components/CvField';
import { validateCvFile, type CvFileError } from '@/features/public/cvFile';
import { toApiError } from '@/lib/problemDetails';
import { getCandidateCvDownloadUrl, type CandidateCv } from '../candidateProfileApi';
import { useRemoveCandidateCv, useUploadCandidateCv } from '../useCandidateProfile';

/* Shared with the apply form: the same file rules produce the same messages wherever a CV is
   chosen. 'file.content_mismatch' has no client-side check — only the server reads magic bytes —
   but it can come back from the upload, so it is mapped here too. */
const FILE_ERROR_KEYS = {
  'file.empty': 'public.apply.fileEmpty',
  'file.too_large': 'public.apply.fileTooLarge',
  'file.unsupported_type': 'public.apply.fileUnsupported',
  'file.content_mismatch': 'public.apply.fileMismatch',
} as const satisfies Record<CvFileError | 'file.content_mismatch', string>;

/* The CV saved to the account, uploaded once and reused on every application. Unlike the apply
   form's field, choosing a file here sends it immediately — there is no surrounding form to submit,
   and a "save" button for a single file would be a step with nothing else in it. */
export function CandidateCvCard({ cv }: { cv: CandidateCv | null }) {
  const { t, i18n } = useTranslation();
  const { toast } = useToast();
  const upload = useUploadCandidateCv();
  const remove = useRemoveCandidateCv();

  const [error, setError] = useState<string | undefined>(undefined);
  const [isPreparingDownload, setIsPreparingDownload] = useState(false);

  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' });

  const select = (file: File) => {
    const code = validateCvFile(file);
    if (code) {
      setError(t(FILE_ERROR_KEYS[code]));
      return;
    }
    setError(undefined);

    upload.mutate(file, {
      onSuccess: () => toast({ title: t('candidateSettings.cv.uploaded'), tone: 'success' }),
      onError: (failure) => {
        const { code: failureCode } = toApiError(failure);
        setError(
          failureCode in FILE_ERROR_KEYS
            ? t(FILE_ERROR_KEYS[failureCode as keyof typeof FILE_ERROR_KEYS])
            : t('candidateSettings.cv.uploadError'),
        );
      },
    });
  };

  /* The signed link is fetched at click time, not held in state: it expires in five minutes, and a
     URL obtained when the page loaded would be dead by the time most people click it. */
  const download = async () => {
    setIsPreparingDownload(true);
    try {
      const { url } = await getCandidateCvDownloadUrl();
      window.open(url, '_blank', 'noopener,noreferrer');
    } catch {
      toast({ title: t('candidateSettings.cv.downloadError'), tone: 'danger' });
    } finally {
      setIsPreparingDownload(false);
    }
  };

  const removeCv = () => {
    remove.mutate(undefined, {
      onSuccess: () => {
        setError(undefined);
        toast({ title: t('candidateSettings.cv.removed'), tone: 'success' });
      },
      onError: () => toast({ title: t('candidateSettings.cv.removeError'), tone: 'danger' }),
    });
  };

  return (
    <Card className="max-w-xl space-y-4">
      <div className="space-y-1">
        <h3 className="text-sm font-semibold text-text">{t('candidateSettings.cv.heading')}</h3>
        <p className="text-sm text-text-muted">{t('candidateSettings.cv.description')}</p>
      </div>

      {cv ? (
        <div className="space-y-3">
          <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-card px-3 py-2.5">
            <div className="min-w-0">
              <p className="truncate text-sm font-medium text-text">{cv.fileName}</p>
              <p className="text-xs text-text-muted">
                {t('candidateSettings.cv.uploadedOn', {
                  date: dateFormatter.format(new Date(cv.uploadedAtUtc)),
                })}
              </p>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <Button
                type="button"
                variant="secondary"
                onClick={download}
                disabled={isPreparingDownload}
              >
                {t('candidateSettings.cv.download')}
              </Button>
              <Button
                type="button"
                variant="ghost"
                onClick={removeCv}
                disabled={remove.isPending || upload.isPending}
              >
                {t('common.remove')}
              </Button>
            </div>
          </div>

          {/* Replacing is the same act as uploading, so it is the same control — no separate
              "replace" mode to get out of. */}
          <p className="text-xs text-text-muted">{t('candidateSettings.cv.replaceHint')}</p>
          <CvDropZone onSelect={select} error={error} disabled={upload.isPending} />
        </div>
      ) : (
        <CvDropZone onSelect={select} error={error} disabled={upload.isPending} />
      )}
    </Card>
  );
}
