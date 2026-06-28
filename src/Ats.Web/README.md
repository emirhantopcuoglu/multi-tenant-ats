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
`http://localhost:5173` (the Vite default). If you change the dev server port, update the API config
accordingly.
