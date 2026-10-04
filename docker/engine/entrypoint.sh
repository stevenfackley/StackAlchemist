#!/bin/sh
# Engine container entrypoint (StackAlchemist#454).
#
# 1. Installs the build user's egress firewall (egress-firewall.sh), per SA_BUILD_EGRESS:
#      required  fail the container when it cannot be installed (prod and CI; compose adds
#                cap NET_ADMIN)
#      optional  warn and continue (the image default, e.g. a plain `docker run`)
#      off       skip
# 2. Drops NET_ADMIN from the bounding set before starting the Engine, so the Engine itself,
#    and anything that compromises it, cannot rewrite those rules.
set -eu

mode="${SA_BUILD_EGRESS:-optional}"
case "$mode" in
    off) echo "[entrypoint] build egress firewall: off" ;;
    required|optional)
        if /usr/local/lib/stackalchemist/egress-firewall.sh; then
            :
        elif [ "$mode" = required ]; then
            echo "[entrypoint] FATAL: the build egress firewall could not be installed. The container needs cap_add NET_ADMIN (see docker-compose.prod.yml)." >&2
            exit 1
        else
            echo "[entrypoint] WARNING: build egress firewall not installed; builds can reach private networks and instance metadata." >&2
        fi
        ;;
    *) echo "[entrypoint] FATAL: SA_BUILD_EGRESS must be required, optional or off (got '$mode')." >&2; exit 1 ;;
esac

if setpriv --bounding-set=-net_admin true 2>/dev/null; then
    exec setpriv --bounding-set=-net_admin -- dotnet StackAlchemist.Engine.dll "$@"
fi
exec dotnet StackAlchemist.Engine.dll "$@"
