import { createContext, useContext } from 'react';
import type { CurrentUser } from '@/types/auth';
import type { Role } from '@/types/enums';
import type {
  CandidateLoginRequest,
  CandidateRegisterRequest,
  LoginRequest,
  RegisterRequest,
} from '@/types/auth';

export interface AuthContextValue {
  /** The signed-in user (company or candidate), or null when anonymous or still loading. */
  user: CurrentUser | null;
  /** The company user's role; null for candidates and anonymous. */
  role: Role | null;
  isAuthenticated: boolean;
  /** True while the initial session check is in flight (avoids guard flicker on reload). */
  isLoading: boolean;
  login: (credentials: LoginRequest) => Promise<void>;
  register: (request: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
  candidateLogin: (credentials: CandidateLoginRequest) => Promise<void>;
  candidateRegister: (request: CandidateRegisterRequest) => Promise<void>;
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
