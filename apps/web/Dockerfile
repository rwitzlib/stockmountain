# Build the production bundle. VITE_* vars are baked in at build time from .env.
FROM node:alpine AS build

WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

# Serve the static bundle with Caddy. The Railway edge proxy forwards
# dev.stockmountain.io here on the same port the Vite dev server used (5173),
# overridable via PORT.
FROM caddy:alpine

COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/dist /srv

EXPOSE 5173
