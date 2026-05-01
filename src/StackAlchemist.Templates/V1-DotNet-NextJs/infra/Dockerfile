# ============================================================
# {{ProjectName}} — Multi-stage, multi-target Dockerfile
# Targets: web | engine
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
COPY nextjs/package.json nextjs/package-lock.json* ./
RUN npm ci --omit=dev

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
RUN addgroup --system --gid 1001 dotnet && adduser --system --uid 1001 --ingroup dotnet dotnet
COPY --from=engine-builder --chown=dotnet:dotnet /app/publish .
USER dotnet
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "{{ProjectName}}.dll"]
