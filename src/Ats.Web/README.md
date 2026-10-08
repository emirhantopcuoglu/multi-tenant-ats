# Ats Web

The single-page application (SPA) front-end for Ats, built with **Vite + React + TypeScript +
Tailwind CSS v4**. It talks to the Ats API over HTTP.

This project is intentionally **not** part of `Ats.sln` — it has its own toolchain (Node/npm) and is
built and deployed separately from the .NET backend.

## Prerequisites

- **Node.js 20.19+ or 22.12+** (developed against the current LTS) and npm.

## Setup

```bash
cd src/Ats.Web
npm install
cp .env.example .env   # then adjust VITE_API_BASE_URL if your API runs elsewhere
```

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Start the Vite dev server with hot reload (default: http://localhost:5173). |
| `npm run build` | Type-check (`tsc -b`) then produce an optimized production build in `dist/`. |
| `npm run preview` | Serve the built `dist/` locally to sanity-check the production bundle. |
| `npm run typecheck` | Type-check only, no build output. |
| `npm test` | Run the Vitest suite once. |
| `npm run test:watch` | Re-run affected tests as files change. |
| `npm run lint` | ESLint over the whole project. |

CI runs `lint`, `test` and `build` in that order — cheapest first. Type-checking is not testing: `tsc`
confirms a function returns a `string`, never that it returns the right one.

## Environment variables

Vite exposes only `VITE_`-prefixed variables to client code, which keeps server-only secrets out of
the bundle. Defined in `.env` (git-ignored); see `.env.example` for the template.

| Variable | Description | Dev default |
|---|---|---|
| `VITE_API_BASE_URL` | Base URL of the Ats API. | `http://localhost:5236` |

## Project structure

```
src/
  app/          App-level providers and composition (router, query client, theme) — added in later steps
  components/   Shared, reusable UI primitives (Button, Input, Table, ...)
  features/     Screen-level modules grouped by domain (jobs, applications, ...)
  lib/          Cross-cutting infrastructure (API client, auth, query client)
  i18n/         Translation dictionaries (tr/en) and the t() helper
  routes/       Route definitions and guards
  styles/       Global CSS and design tokens
  types/        TypeScript types mirroring the backend DTOs/enums
```

## CORS

The API allows this origin via its `Cors:AllowedOrigins` setting. In development that is
`http://localhost:5173` (the Vite default) and `http://localhost:8080` (the Dockerized web below). If
you change the dev server port, update the API config accordingly.

## Serve (production-style, Docker)

The app is served as static files by nginx (`Dockerfile` + `nginx.conf`). The API base URL is **baked
in at build time** — Vite inlines `VITE_*` variables — so it is passed as a build arg, not a runtime
env var:

```bash
docker build --build-arg VITE_API_BASE_URL=https://api.example.com -t ats-web .
docker run -p 8080:80 ats-web
```

Or via the repo's `docker-compose.yml` (serves on `http://localhost:8080`, targeting the host API by
default; override with `VITE_API_BASE_URL`):

```bash
docker compose up web --build
```

nginx handles client-side routing (unknown paths fall back to `index.html`), caches the hashed
`/assets` for a year, and keeps `index.html` uncached so a new deploy is picked up immediately. CI
runs `npm ci && npm run build` on every PR (the `web` job in `.github/workflows/ci.yml`).

## Known `npm audit` finding

`npm audit` reports one high-severity advisory against `react-router`
([GHSA-qwww-vcr4-c8h2](https://github.com/advisories/GHSA-qwww-vcr4-c8h2), CSRF bypass). **It does
not apply here**, and the accepted state is deliberate — read this before "fixing" it.

The advisory says: *"This only affects your application if you are using the unstable RSC APIs."*
This app mounts a plain `<BrowserRouter>` in `src/main.tsx`, ships as static files behind nginx, and
has no `react-router.config.ts` and no React Router server. None of the affected code paths exist in
the bundle.

There is also nothing to upgrade to: the fix is in 8.3.0 and the latest published version is 7.18.2,
which this project already tracks. `npm audit fix --force` "resolves" it by **downgrading** to
7.11.0 — seven releases of unrelated fixes given up to silence an advisory that does not apply.

Re-check when 8.3.0 ships. Until then this is why `npm audit` is not a CI gate: a suppression list
would have to be maintained for one entry, and the reasoning above is the thing worth keeping.

## Deployment notes

- Set `VITE_API_BASE_URL` to the deployed API origin **at build time**.
- Add the web's origin to the API's `Cors:AllowedOrigins` for that environment — production
  `appsettings.json` ships with an empty list on purpose, so each environment must configure its own.
