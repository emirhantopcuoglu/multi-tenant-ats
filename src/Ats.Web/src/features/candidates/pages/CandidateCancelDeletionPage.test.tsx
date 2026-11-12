// @vitest-environment jsdom
import type { ReactNode } from 'react';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ToastProvider } from '@/components/ui';
import { AuthContext, type AuthContextValue } from '@/app/auth/auth-context';
import { CandidateCancelDeletionPage } from './CandidateCancelDeletionPage';

/* i18next is stubbed; `t` returns the key. The layout is stubbed too: its theme and language
   controls need providers this page doesn't. */
vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: 'en' } }),
}));

vi.mock('@/features/auth/components/AuthLayout', () => ({
  AuthLayout: ({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) => (
    <main>
      <h1>{title}</h1>
      <p>{subtitle}</p>
      {children}
    </main>
  ),
}));

vi.mock('../useCandidateAccount', () => ({
  useCancelCandidateAccountDeletion: () => ({ isError: false, isPending: false, mutate: vi.fn() }),
}));

const HOUR_MS = 60 * 60 * 1000;

function renderWithDueDate(dueAtMs: number) {
  const auth = {
    user: {
      kind: 'candidate',
      id: 'candidate-1',
      firstName: 'Mira',
      lastName: 'Tolun',
      email: 'mira@example.test',
      status: 'PendingDeletion',
      deletionDueAtUtc: new Date(dueAtMs).toISOString(),
      isEmailVerified: true,
    },
    isAuthenticated: true,
    isLoading: false,
  } as AuthContextValue;

  render(
    <AuthContext.Provider value={auth}>
      <ToastProvider>
        <MemoryRouter>
          <CandidateCancelDeletionPage />
        </MemoryRouter>
      </ToastProvider>
    </AuthContext.Provider>,
  );
}

const cancelButton = () => screen.queryByRole('button', { name: 'cancelAccountDeletion.confirm' });

describe('CandidateCancelDeletionPage', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('offers to cancel while the grace period lasts', () => {
    renderWithDueDate(Date.now() + HOUR_MS);

    expect(cancelButton()).toBeTruthy();
  });

  /* The API refuses a cancel by then, so the button would only lead to an error. */
  it('says the deletion can no longer be cancelled once the grace period is over', () => {
    renderWithDueDate(Date.now() - HOUR_MS);

    expect(screen.getByText('cancelAccountDeletion.inProgressDescription')).toBeTruthy();
    expect(cancelButton()).toBeNull();
  });
});
