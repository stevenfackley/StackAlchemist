# Implementation Plan: Restore Test Environment Health

This plan addresses the "Error 1033" (Cloudflare Tunnel) and "500 Internal Server Error" observed on the test site by fully deploying the missing backend services and ensuring stable networking.

## Objective
- Build and deploy `sa-engine` and `sa-worker` to the test server.
- Update `sa-web` deployment to be more resilient.
- Ensure all services are on the correct network for the Cloudflare Tunnel.
- Add health checks to `sa-engine` to prevent frontend failures.

## Root Cause Analysis
1.  **Backend Services Missing:** The `deploy-test.yml` workflow only builds and deploys the `sa-web` container. The `sa-engine` (API) and `sa-worker` (Compile service) are not currently managed by CI for the test environment, leading to 500 errors when the frontend tries to call the API.
2.  **Tunnel Connectivity (1033):** The Cloudflare Tunnel (`sa-test-tunnel`) depends on the availability of the origin service. If `sa-web` is down or disconnected from the network during deployment, Cloudflare returns a 1033 error.
3.  **Inconsistent Environment:** The test environment relies on a mix of manual and automated container management.

## Implementation Steps

### 1. Update `docker-compose.yml`
- Add a health check for `sa-engine` (using `wget` which is already in the image).
- Ensure all services use the same network pattern.

### 2. Update `deploy-test.yml`
- **Expand Build Scope:** Modify the workflow to build `sa-engine` and `sa-worker` images in addition to `sa-web`.
- **Switch to Compose-lite:** Instead of raw `docker run`, use a targeted set of commands to build and then update the specific containers.
- **Ensure Engine is Healthy:** Add a step to wait for `sa-engine` to be healthy before finishing the deployment.

### 3. Synchronization & Validation
1.  **Switch to `develop` branch.**
2.  **Apply `docker-compose.yml` changes.**
3.  **Apply `deploy-test.yml` updates.**
4.  **Commit and push to `develop`.**
5.  **Monitor GitHub Actions.** Confirm all 3 containers are built and deployed.
6.  **Verify `test.stackalchemist.app`.**

## Verification & Testing
- **Action Logs:** Confirm `sa-web`, `sa-engine`, and `sa-worker` are all listed as "Up" and "Healthy".
- **Functional Check:** Navigate to the test site, perform a simple generation, and verify the schema extraction works (requires engine).
