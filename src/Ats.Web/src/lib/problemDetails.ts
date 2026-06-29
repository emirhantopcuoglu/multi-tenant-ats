import { AxiosError } from 'axios';
import type { ApiError } from '@/types/api';

/* Shape of an RFC 7807 ProblemDetails body as emitted by ASP.NET's validation pipeline. */
interface ProblemDetailsBody {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

/* Shape of the backend's structured domain error body: { code, message }. */
interface StructuredErrorBody {
  code?: string;
  message?: string;
}

const UNKNOWN_ERROR: ApiError = {
  code: 'unknown_error',
  message: 'Something went wrong. Please try again.',
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

/* Normalize any thrown error into a single ApiError shape (see types/api.ts for why).
   Handles: structured { code, message }, RFC 7807 validation problems, network errors with no
   response, and non-Axios errors — so callers can render `error.message` without branching. */
export function toApiError(error: unknown): ApiError {
  if (error instanceof AxiosError) {
    // No response at all: the request never reached the server (offline, CORS, DNS).
    if (!error.response) {
      return { code: 'network_error', message: error.message || UNKNOWN_ERROR.message };
    }

    const data = error.response.data;
    if (isRecord(data)) {
      // Validation problem: surface both a summary message and the per-field messages.
      const problem = data as ProblemDetailsBody;
      if (problem.errors && isRecord(problem.errors)) {
        return {
          code: 'validation_error',
          message: problem.title ?? 'One or more fields are invalid.',
          fieldErrors: problem.errors,
        };
      }

      // Structured domain error: { code, message }.
      const structured = data as StructuredErrorBody;
      if (structured.code || structured.message) {
        return {
          code: structured.code ?? 'error',
          message: structured.message ?? UNKNOWN_ERROR.message,
        };
      }
    }

    // Responded, but in a shape we don't recognise — fall back to the HTTP status text.
    return { code: `http_${error.response.status}`, message: error.response.statusText || UNKNOWN_ERROR.message };
  }

  if (error instanceof Error) {
    return { code: 'client_error', message: error.message };
  }

  return UNKNOWN_ERROR;
}
