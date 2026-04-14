# Implementation Plan: Fix Production Build Failure (sa-web Health Check)

This plan addresses the root cause of the failed production deployment: the `sa-web` container's health check failing because `wget` is missing from its base image. It also resolves a build-time warning about `git` being missing during the Next.js build.

## Objective
- Resolve production deployment failure by ensuring `sa-web` health checks pass.
- Eliminate build-time warnings by adding necessary dependencies to the `Dockerfile`.
- Maintain synchronization across `main` and `develop` branches.
- Update documentation to reflect the production health check requirements.

## Key Files & Context
- `Dockerfile`: Multi-stage build for all components.
- `docker-compose.prod.yml`: Production configuration with health checks.
- `docs/architecture/Software Design Document.md`: Architectural documentation.
- `docs/architecture/Dev Environment Setup.md`: Environment setup documentation.

## Root Cause Analysis
- The `sa-web` container in `docker-compose.prod.yml` uses a `healthcheck` that executes `wget`.
- The `web` stage in the `Dockerfile` uses `node:20-alpine`, which does not include `wget` by default.
- Additionally, the Next.js build (`npx next build`) emitted a warning that `git` was not found in the `web-builder` stage.

## Implementation Steps

### 1. Update `Dockerfile`
- **Modify `web-builder` stage:** Add `apk add --no-cache git` to support build-time telemetry/metadata requirements.
- **Modify `web` stage:** Add `apk add --no-cache wget` to support the production health check.

### 2. Update Documentation
- **Modify `docs/architecture/Software Design Document.md`:** Add a note about the health check dependencies for the Web component.
- **Modify `docs/architecture/Dev Environment Setup.md`:** Update the "Containerization" section to reflect the new dependencies.

### 3. Synchronization & Deployment
1.  **Switch to `develop` branch.**
2.  **Apply `Dockerfile` changes.**
3.  **Apply Documentation changes.**
4.  **Commit and push to `develop`.** Verify test site deployment.
5.  **Merge `develop` into `main`.**
6.  **Push to `main`.** Verify production deployment.

## Verification & Testing
- **Local Build:** Run `docker compose build sa-web` to ensure the image builds without `git` warnings.
- **Health Check Simulation:** Start the container locally and run `docker exec sa-web wget -qO /dev/null http://localhost:3000/` to verify `wget` works.
- **GitHub Actions:** Confirm that the "Deploy Production Site" workflow passes.
