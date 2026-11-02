import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios';
import { describe, expect, it } from 'vitest';
import { isConcurrencyConflict } from './problemDetails';

function httpError(status: number, data: unknown): AxiosError {
  const response = {
    status,
    statusText: '',
    data,
    headers: {},
    config: { headers: new AxiosHeaders() },
  } as AxiosResponse;
  return new AxiosError('Request failed', undefined, undefined, undefined, response);
}

describe('isConcurrencyConflict', () => {
  it('recognises the 409 the API sends when someone else saved first', () => {
    const error = httpError(409, {
      title: 'This record was changed by someone else. Reload and try again.',
      status: 409,
      code: 'concurrency_conflict',
    });

    expect(isConcurrencyConflict(error)).toBe(true);
  });

  it('ignores other failures, including other 409s', () => {
    expect(isConcurrencyConflict(httpError(409, { code: 'application.duplicate' }))).toBe(false);
    expect(isConcurrencyConflict(httpError(400, { code: 'application.invalid_operation' }))).toBe(false);
    expect(isConcurrencyConflict(new Error('offline'))).toBe(false);
  });
});
