#!/bin/sh
# Egress rules for the build user, uid 10001 (StackAlchemist#454). Applied once by the
# entrypoint, inside the engine container's own network namespace; needs cap NET_ADMIN.
#
# Builds need the public package registries (npm, NuGet, PyPI) and nothing else. Everything a
# build could use to reach the platform is private address space: the compose network (sa-web,
# nginx), the host, the VPC, and the cloud instance-metadata service (169.254.169.254), which
# hands out credentials. Those are rejected for uid 10001 only; the Engine (root) is unaffected.
# DNS is the one exception: the resolvers in /etc/resolv.conf stay reachable on port 53.
set -eu

BUILD_UID=10001
CHAIN=SA_BUILD_EGRESS

v4_blocked="0.0.0.0/8 10.0.0.0/8 100.64.0.0/10 127.0.0.0/8 169.254.0.0/16 172.16.0.0/12 192.0.0.0/24 192.168.0.0/16 198.18.0.0/15 224.0.0.0/4 240.0.0.0/4"
v6_blocked="::1/128 ::ffff:0:0/96 64:ff9b::/96 fc00::/7 fe80::/10 ff00::/8"

resolvers() { awk '/^nameserver[ \t]/ { print $2 }' /etc/resolv.conf 2>/dev/null || true; }

apply() { # <iptables binary> <blocked ranges> <address family filter>
    ipt=$1
    $ipt -w -N "$CHAIN" 2>/dev/null || $ipt -w -F "$CHAIN"
    for ns in $(resolvers); do
        case "$ns" in *:*) [ "$3" = 6 ] || continue ;; *) [ "$3" = 4 ] || continue ;; esac
        if [ "$ns" = 127.0.0.11 ]; then
            # Docker's embedded DNS: its port 53 is DNAT-ed to a random port before this
            # filter sees the packet, so allow the address, not the port.
            $ipt -w -A "$CHAIN" -d "$ns" -j RETURN
        else
            $ipt -w -A "$CHAIN" -d "$ns" -p udp --dport 53 -j RETURN
            $ipt -w -A "$CHAIN" -d "$ns" -p tcp --dport 53 -j RETURN
        fi
    done
    for net in $2; do
        $ipt -w -A "$CHAIN" -d "$net" -j REJECT
    done
    $ipt -w -C OUTPUT -m owner --uid-owner "$BUILD_UID" -j "$CHAIN" 2>/dev/null \
        || $ipt -w -I OUTPUT 1 -m owner --uid-owner "$BUILD_UID" -j "$CHAIN"
}

apply iptables "$v4_blocked" 4

# IPv6 is best effort: compose networks are IPv4-only unless enabled, and a kernel without
# ip6tables support has no IPv6 route out of this namespace to protect.
if apply ip6tables "$v6_blocked" 6 2>/dev/null; then
    echo "[egress-firewall] IPv4 and IPv6 rules applied for uid $BUILD_UID"
else
    echo "[egress-firewall] IPv4 rules applied for uid $BUILD_UID (IPv6 rules unavailable)"
fi
