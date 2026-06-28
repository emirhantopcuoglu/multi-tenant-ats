/// <reference types="vite/client" />

// Typed access to the build-time environment. Vite only exposes variables prefixed with VITE_ to
// client code (import.meta.env.VITE_API_BASE_URL), which keeps server-only secrets out of the bundle.
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
