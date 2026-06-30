import { useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/cn';
import { CV_ACCEPT } from '../cvFile';

interface CvUploadProps {
  file: File | null;
  onSelect: (file: File) => void;
  onClear: () => void;
  error?: string;
}

/* CV drop zone: click-to-browse or drag-and-drop, with the selected file shown as a removable chip.
   It only reports the chosen file upward; validation (type/size) lives with the form so the same
   rule covers both a dropped and a browsed file. The hidden native input keeps the control fully
   keyboard- and screen-reader-accessible. */
export function CvUpload({ file, onSelect, onClear, error }: CvUploadProps) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);
  const errorId = useId();
  const [isDragging, setIsDragging] = useState(false);

  const handleFiles = (files: FileList | null) => {
    const picked = files?.[0];
    if (picked) onSelect(picked);
  };

  if (file) {
    return (
      <div className="space-y-1.5">
        <span className="block text-sm font-medium text-text">{t('public.apply.cv')}</span>
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
      </div>
    );
  }

  return (
    <div className="space-y-1.5">
      <span className="block text-sm font-medium text-text">{t('public.apply.cv')}</span>
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        onDragOver={(event) => {
          event.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={(event) => {
          event.preventDefault();
          setIsDragging(false);
          handleFiles(event.dataTransfer.files);
        }}
        aria-describedby={error ? errorId : undefined}
        className={cn(
          'flex w-full flex-col items-center gap-1 rounded-lg border border-dashed px-4 py-8 text-center transition-colors',
          isDragging ? 'border-accent bg-accent-subtle' : 'border-border hover:border-accent',
          error && 'border-danger',
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

/* Human-readable size: KB under a megabyte, MB above, one decimal so a 9.4 MB file reads clearly
   against the 10 MB ceiling. */
function formatFileSize(bytes: number): string {
  const kb = bytes / 1024;
  if (kb < 1024) return `${Math.round(kb)} KB`;
  return `${(kb / 1024).toFixed(1)} MB`;
}
