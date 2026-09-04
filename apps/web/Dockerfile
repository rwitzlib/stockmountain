# Build the production bundle.
FROM node:alpine AS build

WORKDIR /app

COPY package*.json ./
RUN npm ci

# Vite inlines import.meta.env.VITE_* at build time, so these must be present
# HERE, not at container start. Railway forwards service variables to the image
# build as --build-arg; Docker drops them unless each one is declared as an ARG.
# Adding a new VITE_* variable means adding it in all three places below.
ARG VITE_CLERK_PUBLISHABLE_KEY
ARG VITE_API_URL
ENV VITE_CLERK_PUBLISHABLE_KEY=$VITE_CLERK_PUBLISHABLE_KEY
ENV VITE_API_URL=$VITE_API_URL

# Fail the build rather than shipping a bundle that white-screens on load with
# "Missing VITE_CLERK_PUBLISHABLE_KEY" — an unset var inlines as `undefined`.
RUN test -n "$VITE_CLERK_PUBLISHABLE_KEY" \
    || (echo "ERROR: VITE_CLERK_PUBLISHABLE_KEY is not set at build time." >&2 && exit 1)

COPY . .
RUN npm run build

# Serve the static bundle with Caddy. The Railway edge proxy forwards
# dev.stockmountain.io here on the same port the Vite dev server used (5173),
# overridable via PORT.
FROM caddy:alpine

COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/dist /srv

EXPOSE 5173
