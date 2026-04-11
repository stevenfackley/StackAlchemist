# CI/CD Build Monitor Report

**Generated:** 2026-04-10 (scheduled-task run)
**Repository:** stevenfackley/StackAlchemist
**Inspected HEAD (main):** `1e287d14` — *chore(release): auto-update changelog for 1bfa4bdc*
**Monitoring agent note:** `gh` CLI was not installed in the sandbox and no GitHub token
was available, so this run used the unauthenticated public GitHub REST API to inspect
workflow/job metadata. Log payloads (the `/logs` endpoint) require auth and could **not**
be downloaded. Diagnosis is therefore based on job/step metadata + source inspection of
the workflow files at `origin/main` / `origin/develop`.

---

## 1. Workflow status (last 15 runs)

| Run ID | Workflow | Branch | Event | Conclusion |
|---|---|---|---|---|
| 24232520560 | CI | main | schedule | **failure** |
| 24227125418 | Deploy Test Site | develop | push | **failure** |
| 24178668604 | CI | main | schedule | **failure** |
| 24169236194 | Deploy Production Site | main | push | cancelled |
| 24169236180 | CI | main | push | **failure** |
| 24169236165 | Release | main | push | success |
| 24168911541 | CI | fix/ci-e2e-integration | pull_request | success |
| 24167936691 | CI | codex/ci-root-cause-stabilization | pull_request | success |
| 24123369570 | CI | main | schedule | failure |
| 24118193123 | Deploy Production Site | main | push | cancelled |
| 24118193111 | Release | main | push | success |
| 24118193110 | CI | main | push | failure |
| 24118119347 | CI | codex/ci-root-cause-stabilization | pull_request | success |
| 24114849563 | Release | main | push | success |
| 24114849561 | Deploy Production Site | main | push | cancelled |

Three failing workflows are in play: `CI` (on main push + nightly schedule),
`Deploy Test Site` (on develop push), and `Deploy Production Site` (persistently
cancelled — same self-hosted-runner pattern documented in the 2026-04-08 report).

---

## 2. Issue A — `CI` / `E2E Integration (Playwright, Main/Nightly)`

### Failing job / step
Every recent `CI` run on `main` (both `push` and `schedule` events) fails in exactly
the same place:

```
Job:  E2E Integration (Playwright, Main/Nightly)
Step: Validate Required Integration Secrets      ❌ failure
```

All preceding jobs (`frontend`, `backend`, `docker` x3) are green. The `e2e-smoke` job
is correctly skipped on the scheduled event. The integration job is gated behind
`environment: Test` and `needs: [frontend, backend, docker]`, and it reaches the
validation step before any test execution.

### Why the prior fix did not resolve it
Commit `ee6bff0d` (merged via PR #6, `fix/ci-e2e-integration`) introduced the
`Validate Required Integration Secrets` step that now fails. That step is exactly
what we'd want — it `grep`s the generated `.env` for each required key, rejects empty
values, rejects placeholder strings (`your-`, `placeholder`, `changeme`, …), and
pattern-matches each key's expected format (Supabase JWT shape, `sk-ant-` for
Anthropic, `whsec_` / `sk_` / `pk_` for Stripe, 32-hex Cloudflare account ID, etc.).

The `fix/ci-e2e-integration` PR run passed **because `e2e-integration` only runs on
`push`-to-`main` or `schedule` — not on `pull_request`.** So the strict validator was
never actually executed against the `Test` environment secrets until the PR was
merged. On `main` it fires, and the `Test` environment secrets either don't exist,
are empty, or still hold placeholder values.

### Root cause
**Infrastructure / secret configuration gap.** One or more of the following secrets
on the GitHub `Test` environment for `stevenfackley/StackAlchemist` are missing,
empty, or placeholders:

```
NEXT_PUBLIC_SUPABASE_URL              (via secrets.SUPABASE_URL)
NEXT_PUBLIC_SUPABASE_ANON_KEY         (via secrets.SUPABASE_ANON_KEY)
SUPABASE_SERVICE_ROLE_KEY             (via secrets.SUPABASE_SERVICE_ROLE_KEY)
STRIPE_WEBHOOK_SECRET                 (via secrets.STRIPE_WEBHOOK_SECRET)
STRIPE_SECRET_KEY                     (via secrets.STRIPE_SECRET_KEY)
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY    (via secrets.STRIPE_PUBLISHABLE_KEY)
R2_ACCESS_KEY_ID                      (via secrets.R2_ACCESS_KEY_ID)
R2_SECRET_ACCESS_KEY                  (via secrets.R2_SECRET_ACCESS_KEY)
CLOUDFLARE_TUNNEL_TOKEN               (via secrets.CLOUDFLARE_TUNNEL_TOKEN)
ANTHROPIC_API_KEY                     (via secrets.ANTHROPIC_API_KEY)
R2_ACCOUNT_ID                         (via secrets.CF_ACCOUNT_ID → R2_ACCOUNT_ID)
ENGINE_SERVICE_KEY                    (via secrets.ENGINE_SERVICE_KEY)
```

Without authenticated log access I cannot identify precisely which key(s) tripped
the validator. However, the source of the failure is unambiguous: **no code change
can satisfy `Validate Required Integration Secrets` — it is guarding against exactly
the situation that exists.**

### Required action (cannot be performed by this agent)
Per scheduled-task constraints (*"If a failure requires secrets, environment
variables, or infrastructure changes you cannot make, document the issue clearly and
stop — do not attempt workarounds"*), the fix is **owner-side**:

1. Go to **GitHub → Settings → Environments → Test** for `stevenfackley/StackAlchemist`.
2. Populate every secret listed above with real (test-tier) values. Ensure:
   - `SUPABASE_URL` matches `^https://.+\.supabase\.co/?$`.
   - `SUPABASE_ANON_KEY` / `SUPABASE_SERVICE_ROLE_KEY` are JWTs (three dot-separated
     segments).
   - `STRIPE_*` keys use the correct `whsec_` / `sk_` / `pk_` prefixes.
   - `ANTHROPIC_API_KEY` starts with `sk-ant-`.
   - `CF_ACCOUNT_ID` is a 32-char hex string.
   - `ENGINE_SERVICE_KEY`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`,
     `CLOUDFLARE_TUNNEL_TOKEN` meet the length-minimum patterns.
3. Re-run the failed `CI` workflow on `main` (or wait for the next nightly schedule
   at `0 6 * * *`).

### Optional code-level follow-ups (not applied)
These would make failures easier to diagnose, but none *fix* the current failure:

- **Emit which keys failed, not just that validation failed.** The validator already
  writes `::error::` annotations — but those are only visible in logs, which
  requires authenticated access. Consider also appending a one-line
  "Missing/invalid keys: X, Y, Z" summary to `$GITHUB_STEP_SUMMARY` so owners can
  triage from the run summary page.
- **Promote the validator to a separate job that runs on `pull_request` too** (when
  `.github/**` changes). A PR that would break integration can then be caught before
  merge, instead of only after main breaks.
- **Fail fast with a block of `::notice::` instructions** telling the on-call engineer
  exactly where to configure the missing secrets.

None of these were committed because (a) I have no GitHub token to push, and
(b) the task instructions say to stop when the blocker is a secrets/infrastructure
gap.

---

## 3. Issue B — `Deploy Test Site` (run 24227125418, branch `develop`)

### Failing job / step
```
Job:  Deploy Web App to Test Server     (runs-on: self-hosted, environment: test)
Steps completed: Set up job, Checkout, Setup .env (test), Docker pre-flight cleanup
Step 5 "Build Docker images"            ❌  (no conclusion returned via API — started
                                              but did not finish; every following step
                                              is reported as not-run)
```

### Context
`deploy-test.yml` runs on the self-hosted test EC2 runner. Step 5 does a cold docker
build (`--pull --no-cache`) of the `engine`, `worker`, and `web` targets from the
multi-stage root `Dockerfile`. Because the step terminated without a conclusion, the
most likely causes are:

1. **Out-of-disk / OOM on the self-hosted runner.** `--pull --no-cache` for all three
   targets is expensive on a constrained test box, and the previous 2026-04-08 report
   already flagged that the self-hosted runner fleet is unstable (Deploy Production
   Site is perpetually stuck/cancelled on the same runner pool).
2. **Docker daemon restart / socket hang-up** mid-build.
3. **Transient network failure pulling a base image** — forced by `--pull` on every
   build.
4. **Build-time regression in the Dockerfile** triggered by a change on `develop`.
   Less likely, since the same `Dockerfile` is passing in the `docker` job on `main`
   (which uses Buildx + GHA cache), but `develop` may have drifted.

### Why I cannot confirm root cause from here
The `/actions/runs/{id}/logs` endpoint returns HTTP 403 without auth, and this
scheduled task has no GitHub token. I also can't `ssh` onto the self-hosted runner.
Without the step log we cannot tell a Dockerfile regression apart from a runner-host
problem — and the fixes are completely different.

### Required action (owner-side)
1. Open run `24227125418` in the GitHub UI and read the full "Build Docker images"
   step log.
2. If the error is `no space left on device`, `Cannot connect to the Docker daemon`,
   `killed`, `context deadline exceeded`, `TLS handshake timeout`, etc. → this is a
   runner host problem. On the self-hosted runner:
   - `df -h` + `docker system df`; run `docker system prune -af --volumes` if needed.
   - `sudo systemctl status actions.runner.* docker` — confirm both are healthy.
   - Restart the runner service if it is flapping.
3. If the error is a Dockerfile/build regression (e.g. `dotnet restore` failure, a
   missing file, pnpm resolution error) → fix on `develop` with a `fix(build):`
   commit.
4. Incidental hardening: step 5 invokes three serial `docker build --no-cache` calls
   against the same multi-stage Dockerfile. Consider switching to
   `docker buildx bake` or a single multi-target build plan to cut build time ~3x
   and shrink the window for a transient failure.

---

## 4. Issue C — `Deploy Production Site` persistently `cancelled`

Runs `24169236194`, `24118193123`, and `24114849561` all show `cancelled` on `main`
pushes. This matches the "stuck self-hosted runner" pattern documented in the
2026-04-08 report. Confirmed root cause (from the earlier report + current metadata):
the `self-hosted, linux, ARM64, prod`-labeled runner is not picking up jobs, and the
concurrency group eventually cancels them.

**Required action:** the infrastructure remediation from the 2026-04-08 report still
applies — verify the prod EC2 runner is online, the `actions.runner.*` systemd unit
is up, and that it is registered with matching labels. Re-provision per
`docs/advanced-docs/prod-ec2-runner-and-oidc.md` if the EC2 instance was terminated.

---

## 5. What this agent did NOT change

No commits were authored, no branches were pushed, and no patches were applied
during this run. Justification:

- **Issue A** is a secrets-configuration gap in the GitHub `Test` environment. Per
  the scheduled-task contract, blockers that require secrets or infrastructure
  changes should be reported, not worked around. Loosening or removing the
  `Validate Required Integration Secrets` step would be a band-aid that defeats the
  whole point of PR #6.
- **Issue B** cannot be diagnosed to root cause without the step log. Committing a
  speculative change would risk making things worse and violate the "no band-aid"
  mandate.
- **Issue C** is a runner-fleet / infrastructure problem, not code.
- **No GitHub auth token is available in this sandbox**, so even a committed fix
  could not have been pushed to `origin`.

---

## 6. Recommended follow-ups for the developer

1. **Immediate (unblocks CI on main):** populate the twelve required secrets in the
   `Test` GitHub environment (see list in §2), then re-run the latest failed `CI`
   workflow on `main`.
2. **Immediate (unblocks develop → test deploy):** read the log of run `24227125418`,
   triage the self-hosted test runner per §3, and re-push the `develop` tip (or
   re-run the failed workflow).
3. **Immediate (unblocks prod deploy):** bring the prod self-hosted runner back
   online per the 2026-04-08 report's Issue 2.
4. **Hardening (optional, not applied):**
   - Add a `GITHUB_STEP_SUMMARY` block to `Validate Required Integration Secrets`
     listing each missing/invalid key by name so the failure is diagnosable from the
     run summary without drilling into logs.
   - Add a lightweight `validate-secrets-schema` job that runs on `pull_request`
     when `.github/**` files change, so a misnamed secret reference is caught
     pre-merge.
   - Replace the three serial `docker build --no-cache` calls in `deploy-test.yml`
     with a single `docker buildx bake` targeting `engine`/`worker`/`web` to shorten
     the test deploy and shrink the transient-failure surface area.
   - Provision a GitHub token (or a PAT as an Actions secret for use by this
     scheduled task) so the next automated run can download logs, push small fixes
     automatically, and actually close the loop end-to-end.

---

## 7. Final status

**All CI/CD builds are NOT green.** Three workflows are failing (`CI`,
`Deploy Test Site`, `Deploy Production Site`), and all three root causes lie outside
the code — specifically, in GitHub Environment secrets and self-hosted runner
health. No commits were made this run; owner action is required per §6.
