# ============================================================
# {{ProjectName}} — Multi-stage, multi-target Dockerfile
# Targets: web | engine
#
# Build context is the PROJECT ROOT (the directory holding dotnet/ and nextjs/),
# which is where this file and docker-compose.yml live. `docker compose up --build`
# from that directory is the supported entry point.
# ============================================================

# ---- Base: Node ----
FROM node:24-alpine AS node-base
WORKDIR /app

# ---- Base: .NET SDK ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-sdk

# ============================================================
# TARGET: web
# ============================================================
FROM node-base AS web-deps
COPY nextjs/package.json nextjs/package-lock.json ./
# NOT --omit=dev: `next build` needs typescript, @types/*, tailwindcss, postcss and
# autoprefixer, all of which are devDependencies. Omitting them made the web target
# fail at the build step. Only the runtime image (below) is dependency-free — it runs
# the traced `standalone` output, which bundles what it needs.
RUN npm ci

FROM node-base AS web-builder
COPY nextjs/ ./
COPY --from=web-deps /app/node_modules ./node_modules
RUN npm run build

FROM node:24-alpine AS web
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3000
RUN addgroup --system --gid 1001 nodejs && adduser --system --uid 1001 nextjs
COPY --from=web-builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=web-builder --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=web-builder --chown=nextjs:nodejs /app/public ./public
USER nextjs
EXPOSE 3000
CMD ["node", "server.js"]

# ============================================================
# TARGET: engine
# ============================================================
FROM dotnet-sdk AS engine-builder
WORKDIR /src
COPY dotnet/ ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS engine
WORKDIR /app
# No user is created here on purpose. This image is Ubuntu 24.04 and already ships a
# non-root `app` user (uid/gid 1654) — reuse it. The previous line ran BusyBox's
# `addgroup`/`adduser`, which exist on Alpine (the `web` target above) but not on a
# Debian/Ubuntu base: `docker build --target engine` died at exit 127 before the image
# ever had a user, so the engine half of every shipped archive was unbuildable.
COPY --from=engine-builder --chown=app:app /app/publish .
USER app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "{{ProjectName}}.dll"]
