import { createContext, useContext } from 'react';
import type { CurrentUser } from '@/types/auth';
import type { Role } from '@/types/enums';
import type { LoginRequest, RegisterRequest } from '@/types/auth';

export interface AuthContextValue {
  /** The signed-in user, or null when anonymous or still loading. */
  user: CurrentUser | null;
  /** Convenience accessor for the user's single role; null when anonymous. */
  role: Role | null;
  isAuthenticated: boolean;
  /** True while the initial /auth/me resolution is in flight (avoids guard flicker on reload). */
  isLoading: boolean;
  login: (credentials: LoginRequest) => Promise<void>;
  /** Create a new tenant + admin and sign in (register returns tokens). */
  register: (request: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
}

/* Separate module (no component export) so React Fast Refresh keeps its boundary. */
export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (context === null) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
