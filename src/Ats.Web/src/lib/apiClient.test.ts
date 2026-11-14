// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { AxiosResponse, InternalAxiosRequestConfig } from 'axios';

const SESSION_KIND_KEY = 'ats-session-kind';
const LEGACY_REFRESH_TOKEN_KEY = 'ats-refresh-token';

/* Plays the browser's cookie jar and the API: the jar is shared by every tab, the cookie is sent
   only with credentials, and each refresh token is accepted once. Slow enough that tabs overlap. */
function createServer() {
  const cookieJar = { refresh: 'refresh-0' };
  let issued = 0;
  const liveAccessTokens = new Set<string>();

  const respond = (config: InternalAxiosRequestConfig, status: number, data: unknown) => {
    const response = { config, status, data, headers: {}, statusText: '' } as AxiosResponse;
    if (status >= 400) {
      return Promise.reject(Object.assign(new Error(`HTTP ${status}`), { config, response }));
    }
    return Promise.resolve(response);
  };

  const adapter = async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
    const sentCookie = config.withCredentials ? cookieJar.refresh : undefined;
    await new Promise((resolve) => setTimeout(resolve, 5));

    if (config.url?.endsWith('/auth/refresh')) {
      if (sentCookie !== cookieJar.refresh) return respond(config, 401, null);
      issued += 1;
      cookieJar.refresh = `refresh-${issued}`;
      liveAccessTokens.add(`access-${issued}`);
      return respond(config, 200, { accessToken: `access-${issued}` });
    }

    const bearer = String(config.headers.Authorization ?? '').replace('Bearer ', '');
    return respond(config, liveAccessTokens.has(bearer) ? 200 : 401, {});
  };

  return { adapter, cookieJar };
}

/* A fresh module graph per tab: own access token and single-flight slot, shared storage. */
async function openTab(server: ReturnType<typeof createServer>) {
  vi.resetModules();
  const axios = (await import('axios')).default;
  axios.defaults.adapter = server.adapter;
  const { apiClient } = await import('./apiClient');
  apiClient.defaults.adapter = server.adapter;
  return apiClient;
}

/* One queue per lock name, which is what navigator.locks gives tabs of the same origin. */
function installWebLocks() {
  const queues = new Map<string, Promise<unknown>>();
  const request = (name: string, callback: () => Promise<unknown>) => {
    const run = (queues.get(name) ?? Promise.resolve()).then(callback);
    queues.set(name, run.catch(() => undefined));
    return run;
  };
  Object.defineProperty(navigator, 'locks', { value: { request }, configurable: true });
}

describe('apiClient refresh', () => {
  beforeEach(() => {
    localStorage.setItem(SESSION_KIND_KEY, 'company');
  });

  afterEach(() => {
    localStorage.clear();
    Reflect.deleteProperty(navigator, 'locks');
  });

  it('lets two tabs refresh in turn instead of spending the same cookie twice', async () => {
    installWebLocks();
    const server = createServer();
    const firstTab = await openTab(server);
    const secondTab = await openTab(server);

    const responses = await Promise.all([
      firstTab.get('/api/v1/jobs'),
      secondTab.get('/api/v1/jobs'),
    ]);

    expect(responses.map((r) => r.status)).toEqual([200, 200]);
    expect(server.cookieJar.refresh).toBe('refresh-2');
  });

  it('still refreshes in a browser without the Web Locks API', async () => {
    const server = createServer();
    const tab = await openTab(server);

    const response = await tab.get('/api/v1/jobs');

    expect(response.status).toBe(200);
    expect(server.cookieJar.refresh).toBe('refresh-1');
  });

  // A script on the page can read localStorage, so no refresh token may end up there.
  it('keeps only the session kind in storage', async () => {
    const tab = await openTab(createServer());

    await tab.get('/api/v1/jobs');

    expect(Object.keys(localStorage)).toEqual([SESSION_KIND_KEY]);
  });

  it('removes a refresh token left in storage by the old client', async () => {
    localStorage.setItem(LEGACY_REFRESH_TOKEN_KEY, 'refresh-from-before');

    await openTab(createServer());

    expect(localStorage.getItem(LEGACY_REFRESH_TOKEN_KEY)).toBeNull();
  });
});
