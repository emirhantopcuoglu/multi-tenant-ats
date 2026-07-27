import { useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/cn';
import type { CandidateCv } from '@/features/candidates/candidateProfileApi';
import { CV_ACCEPT } from '../cvFile';

interface CvDropZoneProps {
  onSelect: (file: File) => void;
  error?: string;
  disabled?: boolean;
}

/* Click-to-browse or drag-and-drop, reporting the chosen file upward and nothing else. Validation
   (type/size) lives with the caller so the same rule covers a dropped and a browsed file alike, and
   so the two callers — the apply form, which holds the file until submit, and the profile card,
   which uploads it immediately — can each do what they need with it. The hidden native input keeps
   the control fully keyboard- and screen-reader-accessible. */
export function CvDropZone({ onSelect, error, disabled }: CvDropZoneProps) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);
  const errorId = useId();
  const [isDragging, setIsDragging] = useState(false);

  const handleFiles = (files: FileList | null) => {
    const picked = files?.[0];
    if (picked) onSelect(picked);
  };

  return (
    <div className="space-y-1.5">
      <button
        type="button"
        disabled={disabled}
        onClick={() => inputRef.current?.click()}
        onDragOver={(event) => {
          event.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={(event) => {
          event.preventDefault();
          setIsDragging(false);
          if (!disabled) handleFiles(event.dataTransfer.files);
        }}
        aria-describedby={error ? errorId : undefined}
        className={cn(
          'flex w-full flex-col items-center gap-1 rounded-lg border border-dashed px-4 py-8 text-center transition-colors',
          isDragging ? 'border-accent bg-accent-subtle' : 'border-border hover:border-accent',
          error && 'border-danger',
          disabled && 'cursor-not-allowed opacity-60',
        )}
      >
        <span className="text-sm font-medium text-text">{t('public.apply.cvPrompt')}</span>
        <span className="text-xs text-text-muted">{t('public.apply.cvHint')}</span>
      </button>
      <input
        ref={inputRef}
        type="file"
        accept={CV_ACCEPT}
        className="hidden"
        onChange={(event) => handleFiles(event.target.files)}
      />
      {error && (
        <p id={errorId} className="text-sm text-danger">
          {error}
        </p>
      )}
    </div>
  );
}

interface ApplyCvFieldProps {
  savedCv: CandidateCv | null;
  useSavedCv: boolean;
  onUseSavedCvChange: (useSaved: boolean) => void;
  file: File | null;
  onSelect: (file: File) => void;
  onClear: () => void;
  error?: string;
}

/* The apply form's CV field. With a CV saved to the account there are two ways to answer, so the
   choice is explicit — an upload control that silently ignores the saved file, or a saved file with
   no way past it, would each be wrong half the time. With nothing saved there is only one way, and
   no radio group is drawn for a choice that does not exist. */
export function ApplyCvField({
  savedCv,
  useSavedCv,
  onUseSavedCvChange,
  file,
  onSelect,
  onClear,
  error,
}: ApplyCvFieldProps) {
  const { t, i18n } = useTranslation();

  const picker = file ? (
    <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-card px-3 py-2.5">
      <div className="min-w-0">
        <p className="truncate text-sm font-medium text-text">{file.name}</p>
        <p className="text-xs text-text-muted">{formatFileSize(file.size)}</p>
      </div>
      <button
        type="button"
        onClick={onClear}
        className="shrink-0 text-sm font-medium text-text-muted transition-colors hover:text-danger"
      >
        {t('public.apply.removeCv')}
      </button>
    </div>
  ) : (
    <CvDropZone onSelect={onSelect} error={error} />
  );

  if (!savedCv) {
    return (
      <div className="space-y-1.5">
        <span className="block text-sm font-medium text-text">{t('public.apply.cv')}</span>
        {picker}
      </div>
    );
  }

  const uploadedOn = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(
    new Date(savedCv.uploadedAtUtc),
  );

  return (
    <fieldset className="space-y-2">
      <legend className="mb-1.5 block text-sm font-medium text-text">{t('public.apply.cv')}</legend>

      <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-border bg-card px-3 py-2.5">
        <input
          type="radio"
          name="cvSource"
          className="mt-1"
          checked={useSavedCv}
          onChange={() => onUseSavedCvChange(true)}
        />
        <span className="min-w-0">
          <span className="block text-sm font-medium text-text">{t('public.apply.useSavedCv')}</span>
          <span className="block truncate text-xs text-text-muted">
            {savedCv.fileName} · {uploadedOn}
          </span>
        </span>
      </label>

      <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-border bg-card px-3 py-2.5">
        <input
          type="radio"
          name="cvSource"
          className="mt-1"
          checked={!useSavedCv}
          onChange={() => onUseSavedCvChange(false)}
        />
        <span className="text-sm font-medium text-text">{t('public.apply.uploadNewCv')}</span>
      </label>

      {!useSavedCv && picker}
    </fieldset>
  );
}

/* Human-readable size: KB under a megabyte, MB above, one decimal so a 9.4 MB file reads clearly
   against the 10 MB ceiling. */
function formatFileSize(bytes: number): string {
  const kb = bytes / 1024;
  if (kb < 1024) return `${Math.round(kb)} KB`;
  return `${(kb / 1024).toFixed(1)} MB`;
}
