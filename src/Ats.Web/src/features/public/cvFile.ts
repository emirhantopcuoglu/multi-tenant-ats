/* Mirrors the backend rules (PDF/DOCX, 10 MB). Only the server can check magic bytes. Codes match
   the backend's, so both use the same messages. */

export const MAX_CV_SIZE_BYTES = 10 * 1024 * 1024;

const ACCEPTED_CV_TYPES = [
  'application/pdf',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
];

export const CV_ACCEPT = `.pdf,.docx,${ACCEPTED_CV_TYPES.join(',')}`;

export type CvFileError = 'file.empty' | 'file.too_large' | 'file.unsupported_type';

/* Convenience only; the content type can be faked. */
export function validateCvFile(file: File): CvFileError | null {
  if (file.size <= 0) return 'file.empty';
  if (file.size > MAX_CV_SIZE_BYTES) return 'file.too_large';
  if (!ACCEPTED_CV_TYPES.includes(file.type)) return 'file.unsupported_type';
  return null;
}
