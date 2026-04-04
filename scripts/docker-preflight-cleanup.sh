#!/usr/bin/env bash
set -Eeuo pipefail

DOCKER_MIN_FREE_GB="${DOCKER_MIN_FREE_GB:-8}"

print_host_free_space() {
  local path="$1"
  if [ -d "$path" ]; then
    echo "==> Host free space for $path"
    df -h "$path" || true
  fi
}

echo "==> Docker disk usage before cleanup"
docker system df || true
print_host_free_space /var/lib/docker
print_host_free_space /var/lib/containerd

echo "==> Pruning dangling build cache/layers/images"
# Safe cleanup: only removes dangling/unreferenced artifacts.
docker builder prune -f || true
docker image prune -f || true
docker container prune -f || true
docker volume prune -f || true

echo "==> Pruning old caches older than 24h"
# Extra pressure relief for constrained LXC disks.
docker builder prune -af --filter "until=24h" || true

if [ -d /var/lib/docker ]; then
  # 4th column from `df -Pk` is available KiB.
  docker_avail_kib="$(df -Pk /var/lib/docker | awk 'NR==2 {print $4}')"
  docker_avail_gb="$((docker_avail_kib / 1024 / 1024))"

  if [ "$docker_avail_gb" -lt "$DOCKER_MIN_FREE_GB" ]; then
    echo "==> Free space still low (${docker_avail_gb}GB < ${DOCKER_MIN_FREE_GB}GB). Running aggressive prune."
    docker system prune -af --volumes || true
  fi
fi

echo "==> Docker disk usage after cleanup"
docker system df || true
print_host_free_space /var/lib/docker
print_host_free_space /var/lib/containerd
