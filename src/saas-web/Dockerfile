FROM node:24-alpine AS deps
WORKDIR /app

# Dependencies in their own layer, so editing source does not reinstall them.
COPY package.json package-lock.json ./
RUN npm ci

FROM node:24-alpine AS build
WORKDIR /app

COPY --from=deps /app/node_modules ./node_modules
COPY . .

# NEXT_PUBLIC_* is inlined into the browser bundle at build time, so it has to
# be a build argument rather than a runtime variable.
ARG NEXT_PUBLIC_API_URL=http://api.localhost
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL

# Placeholders only: these are read per request at runtime, but the build
# imports modules that validate their presence.
ENV AUTH_SECRET=build_time_placeholder \
    AUTH_ISSUER_INTERNAL=http://auth:8080 \
    API_URL_INTERNAL=http://api:8080 \
    OIDC_CLIENT_SECRET=build_time_placeholder \
    NEXT_TELEMETRY_DISABLED=1

RUN npm run build

FROM node:24-alpine AS final
WORKDIR /app

ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

RUN addgroup -g 1001 -S nodejs && adduser -S nextjs -u 1001

# `output: "standalone"` emits a server bundle with only the modules actually
# reached, plus the static assets it expects beside it.
COPY --from=build --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=build --chown=nextjs:nodejs /app/public ./public

USER nextjs
EXPOSE 3000

CMD ["node", "server.js"]
