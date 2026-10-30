/* One error shape for the UI, covering both backend formats: { code, message } and RFC 7807
   validation problems. */
export interface ApiError {
  code: string;
  message: string;
  /* Only for validation failures. */
  fieldErrors?: Record<string, string[]>;
}
