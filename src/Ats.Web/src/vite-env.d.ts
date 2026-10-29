/// <reference types="vite/client" />

// Vite only exposes VITE_ variables to client code.
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
