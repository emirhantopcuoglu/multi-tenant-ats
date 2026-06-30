# Multi-stage: build the SPA with Node, then serve the static output with nginx. The final image
# carries only the compiled assets + nginx — no Node, no source, no node_modules.

FROM node:22-alpine AS build
WORKDIR /app

# Copy the lockfile first so `npm ci` is cached until dependencies actually change.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Vite inlines VITE_-prefixed env vars at build time, so the API base URL is baked into the bundle
# here. It's a build arg (not runtime) for that reason; pass it per environment via the compose build
# args or `docker build --build-arg`.
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:1.27-alpine AS runtime
# Replace the default site with the SPA config (routing fallback + caching).
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
