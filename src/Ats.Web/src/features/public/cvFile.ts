/* CV upload rules, mirrored from the backend boundary (FileSignatureValidator + ApplyController):
   PDF/DOCX only, 10 MB max. The client pre-check spares an obvious round-trip and gives instant
   feedback, but the backend stays the authority — it verifies the real magic bytes, which the
   browser can't. The returned codes match the backend's error codes so both reuse the same i18n
   messages. */

export const MAX_CV_SIZE_BYTES = 10 * 1024 * 1024;

const ACCEPTED_CV_TYPES = [
  'application/pdf',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
];

// `accept` attribute for the file input: extensions plus MIME types, covering both how browsers match.
export const CV_ACCEPT = `.pdf,.docx,${ACCEPTED_CV_TYPES.join(',')}`;

export type CvFileError = 'file.empty' | 'file.too_large' | 'file.unsupported_type';

/* A client-side pre-check. Content-type can be spoofed, so this is convenience only — never the
   security boundary. Returns null when the file passes the cheap checks the browser can do. */
export function validateCvFile(file: File): CvFileError | null {
  if (file.size <= 0) return 'file.empty';
  if (file.size > MAX_CV_SIZE_BYTES) return 'file.too_large';
  if (!ACCEPTED_CV_TYPES.includes(file.type)) return 'file.unsupported_type';
  return null;
}
