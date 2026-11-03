# Ats Web

The web client for Ats: React 19, TypeScript, Vite and Tailwind v4. It's built separately from the
.NET solution and isn't part of `Ats.sln`.

## Setup

Needs Node 20.19+ or 22.12+.

```bash
npm install
cp .env.example .env
npm run dev     # http://localhost:5173
```

`VITE_API_BASE_URL` in `.env` points at the API (default `http://localhost:5236`). The API only
accepts requests from origins listed in its `Cors:AllowedOrigins` setting; dev allows
`http://localhost:5173` and `http://localhost:8080`.

## Scripts

| Command | |
|---|---|
| `npm run dev` | Dev server with hot reload |
| `npm run build` | Type-check and build to `dist/` |
| `npm run preview` | Serve the built `dist/` |
| `npm run typecheck` | Type-check only |
| `npm test` | Run the tests once (`test:watch` to keep watching) |
| `npm run lint` | ESLint |

CI runs lint, test and build.

## Docker

The image builds the app and serves it with nginx. Vite bakes `VITE_*` values into the bundle, so the
API URL is a build arg:

```bash
docker build --build-arg VITE_API_BASE_URL=https://api.example.com -t ats-web .
docker run -p 8080:80 ats-web
```

Or from the repo root: `docker compose up web --build` (http://localhost:8080).

When deploying, also add the web origin to the API's `Cors:AllowedOrigins`. Production config ships
with an empty list.
