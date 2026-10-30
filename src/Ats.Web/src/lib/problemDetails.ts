import { AxiosError } from 'axios';
import type { ApiError } from '@/types/api';

interface ProblemDetailsBody {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

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

/* Turns any thrown error into one ApiError shape, so callers can just render `error.message`. */
export function toApiError(error: unknown): ApiError {
  if (error instanceof AxiosError) {
    // No response: offline, CORS or DNS.
    if (!error.response) {
      return { code: 'network_error', message: error.message || UNKNOWN_ERROR.message };
    }

    const data = error.response.data;
    if (isRecord(data)) {
      const problem = data as ProblemDetailsBody;
      if (problem.errors && isRecord(problem.errors)) {
        return {
          code: 'validation_error',
          message: problem.title ?? 'One or more fields are invalid.',
          fieldErrors: problem.errors,
        };
      }

      const structured = data as StructuredErrorBody;
      if (structured.code || structured.message) {
        return {
          code: structured.code ?? 'error',
          message: structured.message ?? UNKNOWN_ERROR.message,
        };
      }
    }

    // Unknown shape: fall back to the HTTP status text.
    return { code: `http_${error.response.status}`, message: error.response.statusText || UNKNOWN_ERROR.message };
  }

  if (error instanceof Error) {
    return { code: 'client_error', message: error.message };
  }

  return UNKNOWN_ERROR;
}
