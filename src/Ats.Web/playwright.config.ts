import { defineConfig, devices } from '@playwright/test';

/* Runs against an app that is already up: `docker compose --profile app up` locally, the docker-build
   job in CI. MailHog catches the mail, so the flows that need a link from an email can follow it. */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    locale: 'en-US',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
