# Implementation Plan: Restore CI/CD Health

This plan addresses the root causes of the multiple CI failures observed on the `main` branch, ensuring a stable path to production.

## Objective
- Fix the `Dockerfile` syntax error that broke the Docker build validation.
- Synchronize `pnpm-lock.yaml` with `package.json` to fix the frontend CI gate.
- Ensure all fixes are applied to both `develop` and `main` branches.
- Update documentation to prevent future lockfile desynchronization issues.

## Root Cause Analysis
1.  **Docker Failure:** A junk line `t.Worker.dll"]` was accidentally left at the end of the `Dockerfile` (line 80), causing a parse error during build validation.
2.  **Frontend Failure:** The `pnpm-lock.yaml` in `src/StackAlchemist.Web` is out of sync with `package.json` (specifically `styled-jsx@^5.1.6` was added to `package.json` but not persisted in the lockfile). The CI environment's `--frozen-lockfile` check correctly identified this.

## Implementation Steps

### 1. Fix Dockerfile
- Remove the redundant/junk line `t.Worker.dll"]` at the end of the file.

### 2. Synchronize Frontend Lockfile
- Navigate to `src/StackAlchemist.Web`.
- Run `pnpm install` to update `pnpm-lock.yaml`.

### 3. Update Documentation
- **Modify `docs/architecture/Dev Environment Setup.md`:** Add a section under "Git Branching & Release Workflow" or "Frontend Development" regarding the requirement to keep lockfiles in sync before pushing to CI.

### 4. Synchronization & Validation
1.  **Switch to `develop` branch.**
2.  **Apply Dockerfile fix.**
3.  **Apply Lockfile sync.**
4.  **Apply Documentation updates.**
5.  **Commit and push to `develop`.** Confirm that the `Frontend` and `Docker` gates pass in the CI workflow.
6.  **Merge `develop` into `main`.**
7.  **Push to `main`.** Confirm production deployment and release workflows pass.

## Verification & Testing
- **Local Validation:**
    - Run `docker buildx build --target worker .` to verify the Dockerfile is valid.
    - Run `pnpm install --frozen-lockfile` in `src/StackAlchemist.Web` to verify the lockfile is now in sync.
- **GitHub Actions:**
    - Monitor the `CI` and `Deploy Production Site` workflows on GitHub.
