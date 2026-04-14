# Implementation Plan: Automated Production Release Scheme

This plan outlines the transition from a tag-based production deployment to an automated continuous deployment triggered by pushes to the `main` branch. It also includes steps to synchronize the `develop` and `main` branches.

## Objective
- Automate production deployments for every push to the `main` branch.
- Automate changelog generation and GitHub Releases for every push to the `main` branch.
- Synchronize `develop` with `main` to ensure both branches share recent production-specific fixes.
- Maintain existing `develop` to test site deployment.

## Key Files & Context
- `.github/workflows/deploy-prod.yml`: Handles production environment deployment.
- `.github/workflows/release.yml`: Handles changelog generation and GitHub Releases.
- `main` and `develop` branches: Currently diverged with `main` being ahead.

## Implementation Steps

### Phase 1: Branch Synchronization
1.  **Switch to `develop` branch.**
2.  **Merge `main` into `develop`.** This ensures that `develop` includes all the recent "harden deploy pipeline" and "fix nginx api routing" commits currently only on `main`.
3.  **Push `develop` to origin.**

### Phase 2: Workflow Updates
1.  **Modify `deploy-prod.yml`:**
    - Change the `on.push` trigger from `tags: ['v*']` to `branches: [main]`.
    - Keep `workflow_dispatch` for manual triggers.
2.  **Modify `release.yml`:**
    - Change the `on.push` trigger from `tags: ['v*']` to `branches: [main]`.
    - Update the changelog commit message to avoid using `${{ github.ref_name }}` which would be "main" instead of a version tag. Consider using the short SHA or just "auto-release".

### Phase 3: Validation
1.  **Push a test change to `develop`.** Verify `deploy-test.yml` triggers.
2.  **Merge `develop` to `main`.** Verify both `deploy-prod.yml` and `release.yml` trigger automatically.
3.  **Review the generated GitHub Release and `CHANGELOG.md`.**

## Verification & Testing
- Check GitHub Actions tab for successful workflow execution.
- Verify `https://stackalchemist.app` is updated after a push to `main`.
- Verify `https://test.stackalchemist.app` is updated after a push to `develop`.
- Confirm `main` and `develop` branches are in sync after the merge.
