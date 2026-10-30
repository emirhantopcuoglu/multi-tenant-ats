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

// Provider order: theme → query client → toasts → router → AuthProvider (needs the query client
// and the router).
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
