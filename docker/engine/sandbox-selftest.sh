#!/bin/sh
# Proves the Compile Guarantee build sandbox inside a running engine container
# (StackAlchemist#454). Run as root: `docker compose exec -T sa-engine
# /usr/local/lib/stackalchemist/sandbox-selftest.sh`. CI runs it in the E2E Integration lane.
#
# Every check goes through the same wrapper the Engine uses (sa-sandbox-exec), with a fresh
# private HOME under SA_BUILD_ROOT, exactly as BuildSandbox.Apply sets it up.
#
# SELFTEST_PRIVATE_TARGET (host:port) names a private-network peer the build user must NOT
# reach; CI points it at the compose Postgres. Unset or unresolvable skips that one check.
set -u

W=/usr/local/lib/stackalchemist/sa-sandbox-exec
ROOT="${SA_BUILD_ROOT:?SA_BUILD_ROOT is not set}"
JOB="$ROOT/selftest-$$"
HOME_DIR="$JOB.home"
fail=0

pass() { echo "PASS: $1"; }
bad() { echo "FAIL: $1"; fail=1; }

install -d -m 0700 -o 10001 -g 10001 "$JOB" "$HOME_DIR" "$HOME_DIR/tmp"
trap 'rm -rf --one-file-system -- "$JOB" "$HOME_DIR"' EXIT

# Same environment shape the Engine gives a build: a short allowlist, private HOME and TMPDIR.
as_builder() {
    (cd "$JOB" && env -i PATH="$PATH" HOME="$HOME_DIR" TMPDIR="$HOME_DIR/tmp" \
        DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 "$W" "$@")
}

connects() { # <host> <port>: exit 0 when a TCP connection opens within 5 s
    python3 -c 'import socket, sys; socket.create_connection((sys.argv[1], int(sys.argv[2])), 5).close()' "$1" "$2" 2>/dev/null
}
builder_connects() {
    as_builder python3 -c 'import socket, sys; socket.create_connection((sys.argv[1], int(sys.argv[2])), 5).close()' "$1" "$2" 2>/dev/null
}

# ── Identity and privileges ───────────────────────────────────────────────────
[ "$(as_builder id -u)" = 10001 ] && pass "builds run as uid 10001" || bad "builds do not run as uid 10001"
caps=$(as_builder awk '/^CapEff:/ { print $2 }' /proc/self/status)
[ "$caps" = 0000000000000000 ] && pass "no effective capabilities" || bad "effective capabilities: $caps"
nnp=$(as_builder awk '/^NoNewPrivs:/ { print $2 }' /proc/self/status)
[ "$nnp" = 1 ] && pass "no-new-privs is set" || bad "no-new-privs is not set"

# ── The Engine's secrets and other jobs' files ────────────────────────────────
engine=""
for dir in /proc/[0-9]*; do
    if tr '\0' ' ' < "$dir/cmdline" 2>/dev/null | grep -q 'StackAlchemist.Engine.dll'; then
        engine=${dir#/proc/}
        break
    fi
done
if [ -z "$engine" ]; then
    bad "the Engine process was not found"
elif as_builder cat "/proc/$engine/environ" >/dev/null 2>&1; then
    bad "the build user can read the Engine's environment (/proc/$engine/environ)"
else
    pass "the Engine's environment is unreadable"
fi
as_builder cat /proc/1/environ >/dev/null 2>&1 && bad "the build user can read /proc/1/environ" || pass "/proc/1/environ is unreadable"
engine_tmp="${TMPDIR:-/var/lib/stackalchemist/tmp}"
as_builder ls "$engine_tmp" >/dev/null 2>&1 && bad "the build user can list the Engine's job trees ($engine_tmp)" || pass "the Engine's job trees are private"
as_builder ls "$ROOT" >/dev/null 2>&1 && bad "the build user can list $ROOT (other jobs)" || pass "other jobs' build directories cannot be listed"

# ── Network ───────────────────────────────────────────────────────────────────
if [ "${SA_BUILD_EGRESS:-optional}" = off ]; then
    echo "SKIP: network checks (SA_BUILD_EGRESS=off)"
else
    builder_connects 169.254.169.254 80 && bad "the build user reaches instance metadata" || pass "instance metadata is unreachable"
    builder_connects 127.0.0.1 80 && bad "the build user reaches the Engine on loopback" || pass "loopback is unreachable"
    if connects 127.0.0.1 80; then pass "root still reaches loopback (the rules are per-uid)"; else bad "root cannot reach the Engine on loopback"; fi
    target="${SELFTEST_PRIVATE_TARGET:-}"
    if [ -n "$target" ] && connects "${target%:*}" "${target##*:}"; then
        builder_connects "${target%:*}" "${target##*:}" && bad "the build user reaches $target" || pass "private peer $target is unreachable"
    else
        echo "SKIP: private peer check (SELFTEST_PRIVATE_TARGET unset or unreachable from root)"
    fi
    builder_connects registry.npmjs.org 443 && pass "the npm registry is reachable" || bad "the npm registry is unreachable"
fi

# ── The toolchains work as the build user with a fresh HOME ───────────────────
as_builder sh -c 'mkdir -p dn && cd dn && cat > selftest.csproj <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="Humanizer.Core" Version="2.14.1" /></ItemGroup>
</Project>
EOF
echo "System.Console.WriteLine(Humanizer.StringHumanizeExtensions.Humanize(\"self_test\"));" > Program.cs
dotnet build -nologo -v q' >/dev/null 2>&1 && pass "dotnet restore + build (NuGet)" || bad "dotnet restore + build failed"
as_builder sh -c 'mkdir -p js && cd js && echo "{\"name\":\"selftest\",\"version\":\"1.0.0\",\"dependencies\":{\"is-number\":\"7.0.0\"}}" > package.json && npm install --no-audit --no-fund --loglevel=error' >/dev/null 2>&1 \
    && pass "npm install (npm registry)" || bad "npm install failed"
as_builder sh -c 'mkdir -p py && cd py && python -m venv --system-site-packages .venv && .venv/bin/python -m pip install --quiet --disable-pip-version-check six' >/dev/null 2>&1 \
    && pass "python venv + pip install (PyPI)" || bad "python venv + pip install failed"

# ── Nothing outlives the build ────────────────────────────────────────────────
as_builder sh -c 'setsid sleep 600 >/dev/null 2>&1 < /dev/null &' >/dev/null 2>&1
sleep 1
pgrep -u 10001 >/dev/null && pass "a detached build process was running" || bad "the detached test process never started"
"$W" kill -KILL -- -1 >/dev/null 2>&1 || true
sleep 1
pgrep -u 10001 >/dev/null && bad "build-user processes survived the purge" || pass "the purge kills every build-user process"

[ "$fail" = 0 ] && echo "sandbox selftest: all checks passed" || echo "sandbox selftest: FAILED"
exit "$fail"
