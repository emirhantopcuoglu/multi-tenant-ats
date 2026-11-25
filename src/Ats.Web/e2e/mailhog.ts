import { expect, type APIRequestContext } from '@playwright/test';

const MAILHOG_URL = process.env.E2E_MAILHOG_URL ?? 'http://localhost:8025';

interface MailHogMessage {
  Content: { Body: string };
}

/* Waits for the newest mail to `to` and returns the first link in it that starts with `prefix`. The app
   sends plain 8-bit HTML, so the body is read as is. */
export async function linkFromLatestMail(request: APIRequestContext, to: string, prefix: string): Promise<string> {
  let link: string | undefined;
  await expect
    .poll(
      async () => {
        const response = await request.get(`${MAILHOG_URL}/api/v2/search`, { params: { kind: 'to', query: to } });
        const { items } = (await response.json()) as { items: MailHogMessage[] };
        const body = items[0]?.Content.Body;
        link = body?.match(new RegExp(`${prefix}[^"\\s<]+`))?.[0].replace(/&amp;/g, '&');
        return link;
      },
      { timeout: 30_000 },
    )
    .toBeTruthy();
  return link!;
}
