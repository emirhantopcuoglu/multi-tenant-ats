import { expect, test } from '@playwright/test';
import { linkFromLatestMail } from './mailhog';

test('a company can register, confirm its email from the mail and sign in', async ({ page, request }) => {
  const stamp = Date.now();
  const email = `selin.kaya.${stamp}@example.test`;
  const password = 'Test-Password-2026';

  await page.goto('/register');
  await page.getByLabel('Company name').fill('Kuzey Yazilim');
  await page.getByLabel('Workspace URL').fill(`kuzey-${stamp}`);
  await page.getByLabel('First name').fill('Selin');
  await page.getByLabel('Last name').fill('Kaya');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Create workspace' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Check your inbox' })).toBeVisible();

  await page.goto(await linkFromLatestMail(request, email, 'http://localhost:8080/confirm-email'));
  await expect(page.getByRole('heading', { level: 1, name: 'Email address confirmed' })).toBeVisible();

  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
});
