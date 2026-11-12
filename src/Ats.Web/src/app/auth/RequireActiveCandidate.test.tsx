// @vitest-environment jsdom
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';
import { AuthContext, type AuthContextValue } from './auth-context';
import { RequireActiveCandidate } from './RequireActiveCandidate';
import type { CandidateAccountStatus } from '@/types/auth';

function renderAt(status: CandidateAccountStatus) {
  const auth = {
    user: {
      kind: 'candidate',
      id: 'candidate-1',
      firstName: 'Mira',
      lastName: 'Tolun',
      email: 'mira@example.test',
      status,
      deletionDueAtUtc: status === 'PendingDeletion' ? '2026-11-26T15:00:00Z' : null,
      isEmailVerified: true,
    },
    isAuthenticated: true,
    isLoading: false,
  } as AuthContextValue;

  render(
    <AuthContext.Provider value={auth}>
      <MemoryRouter initialEntries={['/candidate/settings']}>
        <Routes>
          <Route path="/candidate/reactivate" element={<p>reactivate screen</p>} />
          <Route path="/candidate/cancel-deletion" element={<p>cancel deletion screen</p>} />
          <Route element={<RequireActiveCandidate />}>
            <Route path="/candidate/settings" element={<p>settings</p>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  );
}

describe('RequireActiveCandidate', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('lets an active account through', () => {
    renderAt('Active');

    expect(screen.getByText('settings')).toBeTruthy();
  });

  it('sends a frozen account to the reactivation screen', () => {
    renderAt('Frozen');

    expect(screen.getByText('reactivate screen')).toBeTruthy();
  });

  it('sends an account waiting for deletion to the cancel screen', () => {
    renderAt('PendingDeletion');

    expect(screen.getByText('cancel deletion screen')).toBeTruthy();
  });
});
