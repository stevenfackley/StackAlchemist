#!/usr/bin/env bash
# scripts/docker-preflight-cleanup.sh
# ---------------------------------------------------------------------------
# Pre-flight Docker cleanup for the self-hosted GitHub Actions runner.
#
# Goals:
#   1. Purge old runner diagnostic logs so the runner can always write new ones.
#   2. Remove ALL unused Docker images (not just dangling) to reclaim disk.
#   3. Prune stopped containers, unused volumes, and stale build cache.
#      Only cache older than 24h is pruned — recent layers speed up warm builds.
#   4. Gate on available disk space — abort early if below MIN_FREE_GB.
#   5. Print disk usage before and after so CI logs are easy to triage.
# ---------------------------------------------------------------------------
set -Eeuo pipefail

# ── Configuration ───────────────────────────────────────────────────────────
# Minimum free disk space required before we attempt a build (gigabytes).
MIN_FREE_GB=2

# Runner diagnostic logs older than this many days are safe to delete.
DIAG_RETAIN_DAYS=3

# Directory where the runner writes its diagnostic logs.
RUNNER_DIAG_DIR="${RUNNER_ROOT:-/actions-runner}/_diag"

# ── Helper ──────────────────────────────────────────────────────────────────
free_gb() {
  # Returns available disk space (in GB, integer) for the filesystem at $1.
  df -BG "$1" 2>/dev/null | awk 'NR==2 {gsub("G",""); print $4}'
}

# ── 0. Before snapshot ──────────────────────────────────────────────────────
echo "── Docker disk usage BEFORE cleanup ──"
docker system df 2>/dev/null || true
echo ""
df -h / 2>/dev/null || true

# ── 1. Purge stale runner diagnostic logs ───────────────────────────────────
echo ""
echo "── Purging runner diagnostic logs older than ${DIAG_RETAIN_DAYS} days ──"
if [ -d "$RUNNER_DIAG_DIR" ]; then
  find "$RUNNER_DIAG_DIR" -maxdepth 1 -name "*.log" -mtime "+${DIAG_RETAIN_DAYS}" -delete 2>/dev/null || true
  echo "Cleaned $RUNNER_DIAG_DIR"
else
  echo "Diag dir not found at $RUNNER_DIAG_DIR — skipping."
fi

# Also clean the runner work temp/tool caches (safe between runs).
for cache_dir in "${RUNNER_ROOT:-/actions-runner}/_work/_temp" "${RUNNER_ROOT:-/actions-runner}/_work/_tool"; do
  if [ -d "$cache_dir" ]; then
    echo "Cleaning runner work cache: $cache_dir"
    rm -rf "${cache_dir:?}"/* 2>/dev/null || true
  fi
done

# ── 2. Remove stopped containers ────────────────────────────────────────────
echo ""
echo "── Pruning stopped containers ──"
docker container prune -f 2>/dev/null || true

# ── 2a. Note: container lifecycle is handled by docker compose up ────────────
# Previous versions force-removed production containers here, causing downtime
# during the entire build phase. Now we let `docker compose up -d` handle
# stopping and replacing containers AFTER images are built, reducing downtime
# to seconds instead of minutes.
echo ""
echo "── Skipping container removal — compose handles container lifecycle ──"

# ── 3. Remove ALL unused Docker images ──────────────────────────────────────
#    This is more aggressive than just dangling=true; it removes any image
#    not referenced by a running or stopped container.
echo ""
echo "── Removing all unused images ──"
docker image prune -af 2>/dev/null || true

# ── 4. Remove unused volumes ────────────────────────────────────────────────
echo ""
echo "── Pruning unused volumes ──"
docker volume prune -f 2>/dev/null || true

# ── 5. Remove stale build cache (>24h old) ──────────────────────────────────
#    Preserve recent cache layers — npm install / dotnet restore layers from the
#    last deploy are reusable and dramatically speed up the next build.
#    Only prune cache older than 24 hours to reclaim disk without cold-building.
echo ""
echo "── Pruning build cache older than 24h ──"
docker builder prune -f --filter "until=24h" 2>/dev/null || true

# ── 6. After snapshot ───────────────────────────────────────────────────────
echo ""
echo "── Docker disk usage AFTER cleanup ──"
docker system df 2>/dev/null || true
echo ""
df -h / 2>/dev/null || true

# ── 7. Disk-space gate ──────────────────────────────────────────────────────
AVAILABLE_GB=$(free_gb /)
echo ""
echo "── Disk space gate (need ≥ ${MIN_FREE_GB} GB, have ${AVAILABLE_GB} GB free) ──"
if [ "${AVAILABLE_GB:-0}" -lt "$MIN_FREE_GB" ]; then
  echo "❌ FATAL: Only ${AVAILABLE_GB} GB free on /. Need at least ${MIN_FREE_GB} GB to proceed."
  echo "   Run 'docker system prune -af --volumes' on the runner to recover space,"
  echo "   or expand the runner's disk."
  exit 1
fi

echo "✅ Pre-flight cleanup complete. ${AVAILABLE_GB} GB available."
