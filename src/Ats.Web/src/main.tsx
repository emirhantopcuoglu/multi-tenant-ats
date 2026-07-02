import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import App from '@/App';
import { ThemeProvider } from '@/app/theme/ThemeProvider';
import { AuthProvider } from '@/app/auth/AuthProvider';
import { ToastProvider } from '@/components/ui';
import { queryClient } from '@/lib/queryClient';
import '@/i18n';
import '@/styles/index.css';

// Entry point: mount the React tree into #root. StrictMode surfaces unsafe lifecycles and
// double-invokes effects in development to flush out side-effect bugs early.
// Provider order: ThemeProvider (UI) → QueryClientProvider (data) → ToastProvider → BrowserRouter
// → AuthProvider (needs both the query client and the router) → App.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <BrowserRouter>
            <AuthProvider>
              <App />
            </AuthProvider>
          </BrowserRouter>
        </ToastProvider>
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
);
