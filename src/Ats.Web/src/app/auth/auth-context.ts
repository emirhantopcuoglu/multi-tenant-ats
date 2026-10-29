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
  user: CurrentUser | null;
  /* Null for candidates and anonymous users. */
  role: Role | null;
  isAuthenticated: boolean;
  /* True during the initial session check, so guards don't flicker on reload. */
  isLoading: boolean;
  login: (credentials: LoginRequest) => Promise<void>;
  /* preferredLanguage is added by the provider from the current UI language. */
  register: (request: Omit<RegisterRequest, 'preferredLanguage'>) => Promise<void>;
  logout: () => Promise<void>;
  candidateLogin: (credentials: CandidateLoginRequest) => Promise<void>;
  candidateRegister: (request: Omit<CandidateRegisterRequest, 'preferredLanguage'>) => Promise<void>;
}

/* Separate module so React Fast Refresh keeps its boundary. */
export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (context === null) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
