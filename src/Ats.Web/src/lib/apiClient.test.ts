// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { AxiosResponse, InternalAxiosRequestConfig } from 'axios';

const REFRESH_TOKEN_KEY = 'ats-refresh-token';

/* Accepts each refresh token once, like the API, and slow enough that two tabs' calls overlap. */
function createServer() {
  let issued = 0;
  let liveRefreshToken = 'refresh-0';
  const liveAccessTokens = new Set<string>();

  const respond = (config: InternalAxiosRequestConfig, status: number, data: unknown) => {
    const response = { config, status, data, headers: {}, statusText: '' } as AxiosResponse;
    if (status >= 400) {
      return Promise.reject(Object.assign(new Error(`HTTP ${status}`), { config, response }));
    }
    return Promise.resolve(response);
  };

  return async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
    await new Promise((resolve) => setTimeout(resolve, 5));

    if (config.url?.endsWith('/auth/refresh')) {
      const { refreshToken } = JSON.parse(config.data as string) as { refreshToken: string };
      if (refreshToken !== liveRefreshToken) return respond(config, 401, null);
      issued += 1;
      liveRefreshToken = `refresh-${issued}`;
      liveAccessTokens.add(`access-${issued}`);
      return respond(config, 200, {
        accessToken: `access-${issued}`,
        refreshToken: liveRefreshToken,
      });
    }

    const bearer = String(config.headers.Authorization ?? '').replace('Bearer ', '');
    return respond(config, liveAccessTokens.has(bearer) ? 200 : 401, {});
  };
}

/* A fresh module graph per tab: own access token and single-flight slot, shared localStorage. */
async function openTab(server: ReturnType<typeof createServer>) {
  vi.resetModules();
  const axios = (await import('axios')).default;
  axios.defaults.adapter = server;
  const { apiClient } = await import('./apiClient');
  apiClient.defaults.adapter = server;
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
    localStorage.setItem(REFRESH_TOKEN_KEY, 'refresh-0');
  });

  afterEach(() => {
    localStorage.clear();
    Reflect.deleteProperty(navigator, 'locks');
  });

  it('lets two tabs refresh in turn instead of spending the same token twice', async () => {
    installWebLocks();
    const server = createServer();
    const firstTab = await openTab(server);
    const secondTab = await openTab(server);

    const responses = await Promise.all([
      firstTab.get('/api/v1/jobs'),
      secondTab.get('/api/v1/jobs'),
    ]);

    expect(responses.map((r) => r.status)).toEqual([200, 200]);
    expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-2');
  });

  it('still refreshes in a browser without the Web Locks API', async () => {
    const tab = await openTab(createServer());

    const response = await tab.get('/api/v1/jobs');

    expect(response.status).toBe(200);
    expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
  });
});
