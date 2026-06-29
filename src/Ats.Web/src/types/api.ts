/* Normalized error the UI works with, regardless of which backend error shape produced it.
   The backend speaks two dialects (see lib/problemDetails.ts):
     1. Structured domain errors: { code, message }   (e.g. invalid credentials, slug taken)
     2. RFC 7807 ProblemDetails:  { title, status, errors: { field: [msgs] } }  (validation)
   We collapse both into this single shape so callers never branch on the source. */
export interface ApiError {
  code: string;
  message: string;
  /** Field-level validation messages, present only for validation (RFC 7807) failures. */
  fieldErrors?: Record<string, string[]>;
}
