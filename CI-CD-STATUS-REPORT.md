# CI/CD Build Monitor Report

**Generated:** 2026-04-14 (scheduled-task run)
**Repository:** stevenfackley/StackAlchemist
**Inspected HEAD (main):** `81681021` — *chore(release): auto-update changelog for 5e9ff6dc*
**Inspected HEAD (develop):** `6857119c` — *fix(ci): rm named containers before compose-up; add maintenance page for both envs*

**Monitoring agent note:** the sandbox did not include `gh` CLI and no GitHub token
was available. The run installed the `gh` binary (v2.62.0) locally but had no
credentials to authenticate, so diagnosis relied on the unauthenticated public
GitHub REST API (workflow/job/step metadata). The `/actions/runs/{id}/logs`
endpoint and the artifact `zip` endpoints both require auth (HTTP 403), so raw
step logs and the `deploy-prod-diagnostics` artifact could not be downloaded.
Diagnosis below is based on step timing + metadata + source inspection of the
workflow and Dockerfile at the inspected HEADs.

---

## 1. Workflow status (last 10 runs)

| Run ID | Workflow | Branch | Event | SHA | Conclusion |
|---|---|---|---|---|---|
| 24387593037 | CI | main | – | – | ✅ success |
| 24383506723 | **Deploy Production Site** | main | workflow_dispatch | `5e9ff6dc` | ❌ **failure (step 10)** |
| 24383505465 | Deploy Production Site | main | push | – | cancelled (superseded) |
| 24383505453 | CI | main | push | – | ✅ success |
| 24383505452 | Release | main | push | – | ✅ success |
| 24383503245 | Deploy Test Site | develop | push | – | ✅ success |
| 24383296338 | **Deploy Production Site** | main | workflow_dispatch | `52b1fb61` | ❌ failure (step 12) |
| 24382609001 | CI | main | push | – | ✅ success |
| 24382608978 | Release | main | push | – | ✅ success |
| 24382608977 | **Deploy Production Site** | main | push | `43543c28` | ❌ failure (cancelled early) |

**Green:** CI (push + nightly), Release, Deploy Test Site.
**Red:** Deploy Production Site — only failing workflow.

Issue A (CI integration secrets) and the Deploy-Test runner outage reported on
2026-04-10 are both resolved: the secrets were populated in the Test
environment and the self-hosted test runner is back. Issue C (Deploy Production
Site) has mutated from "perpetually cancelled" (runner offline) into "runner
online but Docker build failing" — runner is picking up jobs but the production
image build step is dying on the self-hosted ARM64 prod box.

---

## 2. Issue — `Deploy Production Site` failing at Build production images

### Latest run timing (run 24383506723, HEAD `5e9ff6dc`)

```
Job: Deploy Production Stack (self-hosted, linux, ARM64, prod)  → FAILURE
  [1]  Set up job                          2s    success
  [2]  Checkout                            <1s   success
  [3]  Explain OIDC usage                  <1s   success
  [4]  Configure AWS credentials via OIDC  <1s   success
  [5]  Confirm OIDC role assumption        <1s   success
  [6]  Setup .env (prod)                   <1s   success
  [7]  Docker pre-flight cleanup           9s    success
  [8]  Runner diagnostics                  1s    success
  [9]  Verify Docker Compose file          <1s   success
  [10] Build production images             ~10m46s  ⟵ in_progress when job ended (FAILED)
  [11..18] downstream steps                skipped / pending
```

The runner is healthy through step 9 (pre-flight cleanup, `docker version`,
`docker compose config` all pass). Failure occurs inside the serial loop that
runs `docker compose -f docker-compose.prod.yml build sa-web` then `… sa-engine`
(see `.github/workflows/deploy-prod.yml` lines 101–121). Total elapsed time
~10m46s — well under the job-level `timeout-minutes: 45`, so this is not a job
timeout; the bash loop itself hit the `exit 1` branch after its second retry.

### Immediately prior run (24383296338, HEAD `52b1fb61`)

Failed at **step 12 "Build and deploy production stack"** after only ~2 seconds —
classic container-name collision after an unclean reboot. That is exactly what
commit `5e9ff6dc` ("rm named containers before compose-up") was meant to fix.
The very next run on `5e9ff6dc` got past step 12 but then failed in step 10 on
a subsequent workflow_dispatch — progress, but not green.

### Why I cannot pinpoint root cause from the API alone

The `/logs` endpoint returns HTTP 403 without auth. The job-step metadata only
tells us the bash loop reached its terminal `exit 1`. The three realistic
candidate root causes are:

1. **Resource pressure on the ARM64 prod runner** — builder layer cache and
   `node_modules` for a Next.js 15 `standalone` build plus `dotnet publish` on
   two .NET 10 targets is heavy. If `df -h /` or free memory in the "Runner
   diagnostics" step showed low headroom, the build would OOM-kill or `ENOSPC`
   mid-layer. The ARM64 EC2 instance in the 2026-04-08 infra note is a modest
   machine; this is the highest-prior hypothesis.
2. **Transient network failure pulling base images** — the compose build
   effectively performs a full pull on each run. Intermittent `TLS handshake
   timeout` / `context deadline exceeded` against registry-1.docker.io or
   mcr.microsoft.com would fail both retry attempts inside the ~10m window.
3. **Build-time regression triggered by recent changes on main** — less likely:
   the same Dockerfile is building successfully in the `ci.yml` "docker" matrix
   on hosted `ubuntu-latest` (amd64) runners for commit `5e9ff6dc` and
   subsequent. An ARM64-specific regression is possible (e.g. a native dep
   without an `arm64` wheel), but nothing in the recent diff (`feat:
   personalization modal`, `fix(ci): rm named containers`, CHANGELOG bumps)
   touches Dockerfile, package.json, or *.csproj in a way that would plausibly
   break only on ARM64.

Without the step log we cannot distinguish (1) from (2) from (3), and the
remediations are completely different (runner-host triage vs. registry retry
hardening vs. code fix).

---

## 3. What was NOT changed, and why

No commits were authored and nothing was pushed during this run. Justifications
tracked against the scheduled-task constraints:

1. **No GitHub auth token in this sandbox.** Even a staged fix could not be
   pushed to `origin`. `gh` was installed locally but `gh auth status` reports
   no credentials; `git push` would prompt for a username/password that this
   agent does not possess.
2. **Diagnosis is inconclusive without logs.** The task explicitly forbids
   band-aid fixes ("Do NOT apply band-aid fixes. Understand the root cause
   fully"). Committing a speculative change (e.g. adding retry-with-pull-once,
   swapping to `buildx bake`, bumping the job timeout) without knowing whether
   the underlying issue is disk, memory, network, or code would violate that
   constraint and risks masking a real infrastructure problem.
3. **The most-likely cause (runner resource pressure) is an infrastructure
   change this agent cannot make.** Provisioning more disk/RAM on the prod
   ARM64 EC2 instance, pruning its Docker state, or cycling its
   `actions.runner.*` systemd unit are all owner-side actions.

---

## 4. Required owner actions

### 4.1 Unblock prod deploy (highest priority)

Open run `24383506723` in the GitHub UI (Actions → Deploy Production Site) and
read the full log of step 10 "Build production images". The log will resolve
which of the three hypotheses above is correct.

Then:

- **If the error mentions `no space left on device`, `write /var/lib/docker/…`,
  or `ENOSPC`:**
  ```bash
  ssh <prod-ec2-runner>
  df -h /
  docker system df
  docker system prune -af --volumes
  docker builder prune -af
  ```
  Consider expanding the EBS root volume (`aws ec2 modify-volume` +
  `growpart` + `resize2fs`).

- **If the error mentions `killed`, `signal: killed`, `Out of memory`, or
  `cgroup … memory limit`:** the ARM64 runner is OOMing under the Next.js
  build. Options: upsize the instance (e.g. `t4g.medium → t4g.large`), or
  add swap (`fallocate -l 4G /swapfile && mkswap && swapon`). Persist via
  `/etc/fstab`.

- **If the error is a transient `TLS handshake timeout`, `context deadline
  exceeded`, `dial tcp … i/o timeout` against a registry:** harden the
  workflow. The existing retry already covers one retry after a 15s sleep;
  increase to 3 attempts with exponential backoff, and set
  `DOCKER_CLIENT_TIMEOUT=300` / `COMPOSE_HTTP_TIMEOUT=300`.

- **If the error is a real build regression** (e.g. `dotnet restore` failing
  on a package, `pnpm install` resolution conflict, missing file): fix on
  `develop` with a `fix(build): …` commit. Flag it to me with the log excerpt
  and I can scope a targeted patch.

### 4.2 Diagnosability — optional hardening (not applied, needs log first)

When the root cause is known, the following small changes would make the next
failure self-describing without needing log access:

- **Emit `$GITHUB_STEP_SUMMARY` in the build loop** listing per-service build
  time, attempt count, and exit status.
- **Write `docker system df`, `df -h /`, and `free -h` to the summary after
  each failed attempt** so resource-pressure failures are visible from the
  run summary page.
- **Replace the serial loop with `docker buildx bake`** targeting
  `sa-web` and `sa-engine` (and `sa-worker` if it's in `docker-compose.prod.yml`
  — currently only the first two are built). `bake` allows a shared builder
  instance, parallelism control, and a single output surface; combined with
  a registry or local cache it cuts wall-clock time on the ARM64 box.
- **Promote a PR-time Docker build sanity check for `linux/arm64`** (QEMU
  buildx under GHA hosted runners) so ARM64-specific regressions are caught
  before they land on main.

### 4.3 Provision a monitoring token

The prior two reports (2026-04-08 and 2026-04-10) and this one have all been
blocked on the same missing piece: no GitHub token in the scheduled-task
sandbox. If the task is expected to actually close the loop — download logs,
commit fixes, verify re-runs — it needs a fine-grained PAT with:

- `contents: read/write` (so it can push fixes)
- `actions: read` (so it can download logs + artifacts)
- `metadata: read`

scoped to `stevenfackley/StackAlchemist` only, stored as the sandbox's
`GITHUB_TOKEN` environment variable or provided via `gh auth login --with-token`.
Without this, every run will produce a report and stop.

---

## 5. Final status

**All CI/CD builds are NOT green.**

- ✅ `CI` — green on `main` (push + nightly) and on the latest merged PR.
- ✅ `Release` — green.
- ✅ `Deploy Test Site` — green on latest `develop` push.
- ❌ `Deploy Production Site` — **failing at step 10 "Build production images"**
  on the self-hosted ARM64 prod runner. Consistent failure across the last 3
  non-superseded runs. Root cause cannot be confirmed without log access; the
  highest-prior hypothesis is resource pressure on the prod EC2 instance.
  Owner action required per §4.1.

No commits were made by this agent this run (rationale in §3).
