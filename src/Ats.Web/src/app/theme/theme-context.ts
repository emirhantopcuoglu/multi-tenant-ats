import { createContext, useContext } from 'react';

export type Theme = 'light' | 'dark';

export interface ThemeContextValue {
  theme: Theme;
  setTheme: (theme: Theme) => void;
  toggleTheme: () => void;
}

/* Kept in its own module (no component export) so React Fast Refresh stays happy:
   a file that exports both a context/hook and a component loses its refresh boundary. */
export const ThemeContext = createContext<ThemeContextValue | null>(null);

export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext);
  // A null context means a component called useTheme outside the provider — fail loudly
  // at the boundary rather than silently rendering with a missing theme.
  if (context === null) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
}

/* Single source of truth for the persistence key, shared with the anti-FOUC bootstrap
   script in index.html. If you rename this, rename it there too. */
export const THEME_STORAGE_KEY = 'ats-theme';
