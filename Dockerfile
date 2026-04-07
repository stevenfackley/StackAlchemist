# StackAlchemist Multi-Component Dockerfile
# This file uses multi-stage builds and targets to build the Web, Engine, and Worker.

# ==========================================
# STAGE 1: NEXT.JS FRONTEND (WEB)
# ==========================================
FROM node:20-alpine AS web-builder
WORKDIR /app
# Accept public env vars at build time so Next.js bakes them into the bundle
ARG NEXT_PUBLIC_APP_URL=https://test.stackalchemist.app
ENV NEXT_PUBLIC_APP_URL=$NEXT_PUBLIC_APP_URL

# Copy manifests first so the install layer is cached independently of source.
COPY src/StackAlchemist.Web/package.json src/StackAlchemist.Web/package-lock.json ./

# Install all dependencies. --ignore-scripts skips postinstall (setup-env-safe.mjs)
# which requires the full scripts/ dir that isn't copied until the next step.
RUN npm install --include=dev --ignore-scripts

COPY src/StackAlchemist.Web/ .

# Build the Next.js app and clean build cache.
# We invoke next build directly because the npm script wrapper
# (scripts/build-wrapper.mjs) is only needed on Windows+pnpm.
RUN npx next build \
  && rm -rf /root/.npm /tmp/*

FROM node:20-alpine AS web
# Install wget for production health checks
RUN apk add --no-cache wget
WORKDIR /app
ENV NODE_ENV=production
ENV HOSTNAME=0.0.0.0
ENV PORT=3000
COPY --from=web-builder /app/.next/standalone ./
COPY --from=web-builder /app/.next/static ./.next/static
COPY --from=web-builder /app/public ./public
EXPOSE 3000
CMD ["node", "server.js"]

# ==========================================
# STAGE 2: .NET ENGINE (API)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS engine-builder
WORKDIR /src
COPY src/StackAlchemist.Engine/*.csproj ./StackAlchemist.Engine/
RUN dotnet restore ./StackAlchemist.Engine/StackAlchemist.Engine.csproj
COPY src/StackAlchemist.Engine/ ./StackAlchemist.Engine/
RUN dotnet publish ./StackAlchemist.Engine/StackAlchemist.Engine.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS engine
RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=engine-builder /app/publish .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "StackAlchemist.Engine.dll"]

# ==========================================
# STAGE 3: .NET WORKER (COMPILE GUARANTEE)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS worker-builder
WORKDIR /src
# Worker references Engine — copy both .csproj files before restore so the
# dependency graph can be resolved, then copy full source for both projects.
COPY src/StackAlchemist.Engine/*.csproj ./StackAlchemist.Engine/
COPY src/StackAlchemist.Worker/*.csproj ./StackAlchemist.Worker/
RUN dotnet restore ./StackAlchemist.Worker/StackAlchemist.Worker.csproj
COPY src/StackAlchemist.Engine/ ./StackAlchemist.Engine/
COPY src/StackAlchemist.Worker/ ./StackAlchemist.Worker/
RUN dotnet publish ./StackAlchemist.Worker/StackAlchemist.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS worker
# Note: Worker needs SDK to run 'dotnet build' on generated repos
WORKDIR /app
COPY --from=worker-builder /app/publish .
# Install Node.js in the worker container so it can run 'npm build' on generated repos
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && apt-get install -y nodejs
ENTRYPOINT ["dotnet", "StackAlchemist.Worker.dll"]
