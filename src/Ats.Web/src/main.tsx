import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from '@/App';
import { ThemeProvider } from '@/app/theme/ThemeProvider';
import '@/i18n';
import '@/styles/index.css';

// Entry point: mount the React tree into #root. StrictMode surfaces unsafe lifecycles and
// double-invokes effects in development to flush out side-effect bugs early.
// ThemeProvider wraps the app so any screen can read/toggle the theme via useTheme.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <App />
    </ThemeProvider>
  </StrictMode>,
);
