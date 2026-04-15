# Implementation Plan: Fix Test Site Deployment Network & Rollback

This plan addresses the "500 Internal Server Error" observed on the test site by fixing the underlying deployment infrastructure.

## Objective
- Ensure the `under-construction_default` network exists before starting containers.
- Fix the rollback placeholder restoration which is currently failing due to invalid volume mounts.
- Improve diagnostic visibility for the test environment.
- Ensure `sa-web` can reach `sa-engine`.

## Root Cause Analysis
1.  **Network Missing:** The `docker run` command for `sa-web` fails with `network under-construction_default not found`. This happens if the network was pruned or never created on the self-hosted runner.
2.  **Rollback Failure:** The fallback step tries to mount `/root/under-construction/nginx.conf` which Docker identifies as a directory on the host, causing an OCI runtime error when trying to mount it as a file.
3.  **Communication Issue:** Since `sa-web` fails to start, it cannot reach `sa-engine`, leading to 500 errors if the Cloudflare Tunnel is pointing to a dead or missing container.

## Implementation Steps

### 1. Update `deploy-test.yml`
- **Add Network Check:** Add a step to create the `under-construction_default` network if it does not exist.
- **Fix Placeholder Volume Mounts:** Adjust the rollback `docker run` command to use more robust paths or check for existence.
- **Add Engine Presence Check:** Add a step to verify `sa-engine` is running on the same network.

### 2. Update `docker-compose.yml` (Development/Test)
- Add a basic health check to `sa-web` to align with production and improve local diagnostics.

### 3. Synchronization & Validation
1.  **Switch to `develop` branch.**
2.  **Apply `deploy-test.yml` fixes.**
3.  **Apply `docker-compose.yml` updates.**
4.  **Commit and push to `develop`.**
5.  **Monitor GitHub Actions.** Confirm `Deploy Test Site` passes.
6.  **Verify `test.stackalchemist.app/simple`.**

## Verification & Testing
- **GitHub Actions Log:** Verify "✅ Web app responded successfully."
- **Manual Check:** Navigate to `test.stackalchemist.app` and perform a simple generation.
