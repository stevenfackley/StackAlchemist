#!/usr/bin/env bash
# scripts/docker-preflight-cleanup.sh
# ---------------------------------------------------------------------------
# Pre-flight Docker cleanup for the self-hosted GitHub Actions runner.
#
# Goals:
#   1. Remove corrupted "moby-dangling" image references that crash the
#      legacy builder (and can linger even after switching to BuildKit).
#   2. Prune stopped containers, dangling images, and stale build cache
#      so the runner doesn't run out of disk space over time.
#   3. Print disk usage before and after so CI logs are easy to triage.
# ---------------------------------------------------------------------------
set -Eeuo pipefail

echo "── Docker disk usage BEFORE cleanup ──"
docker system df 2>/dev/null || true

# 1. Kill corrupted / dangling image references.
#    The legacy builder sometimes creates "moby-dangling@sha256:…" entries
#    that it later cannot resolve, causing:
#      "creating image moby-dangling@sha256:… already exists, but failed to resolve"
#    Removing ALL dangling images (untagged) eliminates these ghosts.
echo ""
echo "── Removing dangling images ──"
DANGLING=$(docker images -f "dangling=true" -q 2>/dev/null || true)
if [ -n "$DANGLING" ]; then
  echo "$DANGLING" | xargs docker rmi -f 2>/dev/null || true
  echo "Removed dangling images."
else
  echo "No dangling images found."
fi

# 2. Prune stopped containers.
echo ""
echo "── Pruning stopped containers ──"
docker container prune -f 2>/dev/null || true

# 3. Prune build cache older than 12 hours.
echo ""
echo "── Pruning build cache (>12 h) ──"
docker builder prune -f --filter "until=12h" 2>/dev/null || true

# 4. Remove old sa-web-app images that are no longer the :test tag.
echo ""
echo "── Removing stale sa-web-app images ──"
docker images 'sa-web-app' --format '{{.Repository}}:{{.Tag}}' 2>/dev/null \
  | grep -v ':test$' \
  | xargs -r docker rmi -f 2>/dev/null || true

echo ""
echo "── Docker disk usage AFTER cleanup ──"
docker system df 2>/dev/null || true
echo ""
echo "✅ Pre-flight cleanup complete."
