import { expect, test } from '@playwright/test';

test('a candidate can register and stays signed in after a reload', async ({ page }) => {
  const email = `deniz.aksoy.${Date.now()}@example.test`;

  await page.goto('/candidate/register');
  await page.getByLabel('First name').fill('Deniz');
  await page.getByLabel('Last name').fill('Aksoy');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill('a-long-test-password');
  await page.getByRole('button', { name: 'Create account' }).click();

  const myApplications = page.getByRole('link', { name: 'My applications' });
  await expect(myApplications).toBeVisible();

  // The access token lives in memory, so this only passes if the refresh cookie works end to end.
  await page.reload();
  await expect(myApplications).toBeVisible();
});
