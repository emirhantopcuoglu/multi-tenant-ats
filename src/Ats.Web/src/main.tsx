import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from '@/App';
import '@/styles/index.css';

// Entry point: mount the React tree into #root. StrictMode surfaces unsafe lifecycles and
// double-invokes effects in development to flush out side-effect bugs early.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
