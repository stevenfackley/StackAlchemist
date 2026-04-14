# Claude Code Continuation Prompt: Restore CI/CD & Deployment Health

## Current Context
We are troubleshooting persistent GitHub Actions failures across `main` and `develop` branches.
- **Workflow:** `CI` (on `main`) and `Deploy Test Site` (on `develop`) are failing.
- **Goal:** Automate production releases from `main` and test releases from `develop`.

## Identified Issues & Root Causes

### 1. Dockerfile Issues
- **Problem:** `web-builder` stage fails during `npx next build` with `/bin/sh: git: not found`.
- **Cause:** `git` is required by Next.js telemetry/metadata but is missing from the `node:20-alpine` builder image.
- **Action:** Add `apk add --no-cache git` to the `web-builder` stage.

### 2. Test Site Deployment Failure
- **Problem:** `sa-engine` fails the health check in `deploy-test.yml`.
- **Cause A:** The workflow checks `http://localhost:80/health`, but the engine implements `/healthz` in `Program.cs`.
- **Cause B:** .NET 10 (aspnet:10.0) defaults to port **8080** since .NET 8, but the `Dockerfile` and `deploy-test.yml` expect port **80**.
- **Action:**
    - Update `deploy-test.yml` to check `http://localhost:80/healthz`.
    - Explicitly set `ASPNETCORE_URLS=http://+:80` in the `engine` stage of the `Dockerfile` or in the environment variables.

### 3. E2E Test Failures (CI)
- **Problem:** Multiple Playwright tests failing on `main` because elements like "Spark" or "Select Tier" are not visible.
- **Cause:** These are likely cascading failures from the backend services (started via GHCR images in `ci.yml`) not being fully operational or the frontend 500-ing due to API connectivity issues.
- **Action:** Once Docker images and health checks are fixed, re-run E2E tests. Ensure `setup-env` properly populates the `.env` for the E2E environment.

## Task for Next Session
1.  **Switch to `develop` branch.**
2.  **Fix `Dockerfile`:**
    - Add `RUN apk add --no-cache git` to `web-builder`.
    - Add `ENV ASPNETCORE_URLS=http://+:80` to the `engine` stage.
3.  **Fix `deploy-test.yml`:**
    - Change `/health` to `/healthz` in the `Verify Health` step.
4.  **Sync Branches:** Merge `develop` into `main` after verification.
5.  **Verify Production:** Confirm `Deploy Production Site` passes after the fix.

## Implementation Details Reference
- **Engine Health Endpoint:** `GET /healthz` (Returns `{"status": "ok", ...}`)
- **Web App URL (Test):** `https://test.stackalchemist.app`
- **Engine Container:** `sa-engine`, listening internally on 80.
