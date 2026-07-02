import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'node:path';

// Vite is the dev server + build tool. Two plugins:
//   - react(): JSX/Fast Refresh for React.
//   - tailwindcss(): Tailwind v4's first-party Vite plugin (no separate PostCSS config needed).
// The "@" alias lets modules import from the src root (e.g. "@/lib/apiClient") instead of long
// relative paths; the matching TypeScript path is declared in tsconfig.app.json.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      // import.meta.dirname (Node 20.11+) is the ESM-native replacement for __dirname.
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: 5173,
  },
});
