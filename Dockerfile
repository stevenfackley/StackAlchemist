# StackAlchemist Multi-Component Dockerfile
# This file uses multi-stage builds and targets to build the Web, Engine, and Worker.

# ==========================================
# STAGE 1: NEXT.JS FRONTEND (WEB)
# ==========================================
FROM node:20-alpine AS web-builder
WORKDIR /app
COPY src/StackAlchemist.Web/package*.json ./
RUN npm install
COPY src/StackAlchemist.Web/ .
RUN npm run build

FROM node:20-alpine AS web
WORKDIR /app
COPY --from=web-builder /app/.next ./.next
COPY --from=web-builder /app/public ./public
COPY --from=web-builder /app/package*.json ./
RUN npm install --production
EXPOSE 3000
CMD ["npm", "start"]

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
COPY src/StackAlchemist.Worker/*.csproj ./StackAlchemist.Worker/
RUN dotnet restore ./StackAlchemist.Worker/StackAlchemist.Worker.csproj
COPY src/StackAlchemist.Worker/ ./StackAlchemist.Worker/
RUN dotnet publish ./StackAlchemist.Worker/StackAlchemist.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS worker
# Note: Worker needs SDK to run 'dotnet build' on generated repos
WORKDIR /app
COPY --from=worker-builder /app/publish .
# Install Node.js in the worker container so it can run 'npm build' on generated repos
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && apt-get install -y nodejs
ENTRYPOINT ["dotnet", "StackAlchemist.Worker.dll"]
