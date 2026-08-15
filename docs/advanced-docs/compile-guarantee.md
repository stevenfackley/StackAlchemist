# The Compile Guarantee

The Compile Guarantee is the technical promise at the center of StackAlchemist: every Boilerplate and Infrastructure package is verified to compile before delivery. This document explains exactly how it works.

---

## What It Is

For Tier 2 (Boilerplate) and Tier 3 (Infrastructure), before your archive is assembled and made available for download, the generated source code is physically run through the compiler:

- `dotnet build` for the .NET 10 Web API
- `npm run build` for the Next.js 15 frontend

If either build fails, the Compile Guarantee triggers an automatic correction loop. If the code still fails after the maximum number of retries, a full refund is initiated automatically against your original payment method — no dispute, no questions asked, no email to send.

---

## The Pipeline

```
Generation Complete
        │
        ▼
┌─────────────────────────┐
│  Stage Container Boot   │  ← Isolated Docker container with .NET 10 SDK
│                         │     and Node.js 20 (not the user's machine)
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│  dotnet build           │  ← Attempt 1
│  npm run build          │
└───────────┬─────────────┘
            │
     PASS? ─┤
            │
           YES ──────────────────────────────→  Archive & Deliver ✅
            │
           NO
            ▼
┌─────────────────────────┐
│  Parse Build Output     │  ← Extract: file path, line number, error code,
│                         │     error message from compiler stderr
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│  LLM Correction         │  ← Feed compiler errors + affected source files
│                         │     to Claude 3.5 Sonnet. Request targeted fixes.
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│  Apply Patches          │  ← Replace only the files flagged in the error
└───────────┬─────────────┘
            │
            ▼
       Retry Build
       (Attempt 2)
            │
     PASS? ─┤
            │
           YES ──────────────────────────────→  Archive & Deliver ✅
            │
           NO → Retry (Attempt 3)
                      │
               PASS? ─┤
                      │
                     YES ──────────────────→  Archive & Deliver ✅
                      │
                     NO ────────────────────→  Full Refund 💰
```

---

## What Gets Verified

### .NET Build (`dotnet build`)

The build checks:
- All project references resolve correctly
- All `using` directives refer to assemblies that exist
- All types, interfaces, and methods are correctly implemented
- No syntax errors in generated C#
- All repository implementations match their interface contracts
- DI registrations are valid

Common failure modes that the correction loop handles:
- Missing method implementations (interface not fully satisfied)
- Type mismatches (wrong return type on a generated method)
- Missing using directives (LLM referenced a type from an unimported namespace)
- Incorrect Dapper query parameter names

### Next.js Build (`npm ci` → `npm run typecheck` → `npm run build`)

Dependencies are installed with `npm ci` against the lockfile in your archive, falling back
to `npm install` if the correction loop added a dependency. That fallback is a normal,
expected path: when it happens, the `npm ci` attempt is recorded in your report with status
`superseded` and does **not** count against the half — the install that actually ran your
build is what the verdict is based on. `npm run typecheck` (`tsc --noEmit`) runs before
`next build` because it reports every type error at once, where `next build` stops at the
first — so a correction round fixes them all together.

The build checks:
- TypeScript type checking across the entire frontend
- All imported modules exist
- Component props match their TypeScript interfaces
- API client types match the generated TypeScript interfaces
- No `any` type errors (in strict mode)
- Valid JSX and module resolution

Common failure modes:
- Generated TypeScript interface doesn't match API response shape
- Missing properties on generated component props
- Import paths pointing to non-existent modules

### FastAPI + React

If you chose the FastAPI + React/Vite stack, the same guarantee applies to *that* stack, and
your report describes it — a **FastAPI** half and a **React** half, never .NET and Next.js.

- FastAPI half: `pip install -r requirements.txt`, `flake8`, and `pytest --collect-only`
  (every test module imports cleanly)
- React half: `npm install`, `npm run lint`, and `tsc --noEmit`

---

## Why an Isolated Container?

The compile check runs in a clean, isolated Docker container — not the StackAlchemist server, not a shared environment. This isolation guarantees:

1. **No environmental contamination** — A package installed globally on a dev machine can't mask a missing dependency in the generated output
2. **Reproducible builds** — The exact same .NET 10 SDK and Node.js version every time
3. **Security** — User-generated code doesn't execute in a privileged context
4. **Parallelism** — Multiple generations can be compiled simultaneously

---

## The Correction Loop

When a build fails, the correction process is surgical — not a full regeneration.

### What the LLM receives during correction

```
BUILD FAILURE — Attempt 1
========================

Error 1:
  File: api/YourProject.Api/Controllers/ProjectController.cs
  Line: 47
  Error: CS0161 — 'ProjectController.GetByWorkspace': not all code paths return a value

Error 2:
  File: api/YourProject.Data/Repositories/ProjectRepository.cs
  Line: 89
  Error: CS1061 — 'DapperConnection' does not contain a definition for 'QuerySingleAsync'

AFFECTED FILES (full content):
[ProjectController.cs — full file content]
[ProjectRepository.cs — full file content]

TASK: Fix only the specific errors listed above. Do not modify other files.
Return ONLY the corrected file contents.
```

### What the LLM returns

Only the files that need changes. The correction is minimal and targeted — not a regeneration of the entire codebase.

### Retry count rationale

Three retries is the empirically-determined threshold where:
- The vast majority (>99%) of builds that can be fixed are fixed within 2 retries
- The third retry acts as a safety net for edge cases
- Beyond 3 retries, continued failures indicate a structural problem in the schema that the LLM can't reliably self-correct without more context

---

## What the Compile Guarantee Does NOT Cover

The Compile Guarantee is a **compilation guarantee**, not a functional guarantee.

- ✅ Guarantees: code compiles and all type checks pass
- ❌ Does not guarantee: runtime behavior is correct
- ❌ Does not guarantee: generated SQL produces correct query results
- ❌ Does not guarantee: business logic matches your exact intent

Generated code is a starting point — a foundation that's architecturally correct and structurally sound. You own it and you're responsible for its runtime behavior. Think of it like hiring a senior developer to scaffold your project: the scaffold is professional-quality, but you still need to validate the business logic.

---

## Refund Conditions

A refund is initiated automatically when:

1. The generated code fails `dotnet build` or `npm run build`
2. The auto-correction loop has been executed 3 times
3. The build is still failing after all 3 correction attempts

The moment the third attempt fails, the pipeline calls Stripe to refund the original charge in full — no manual step, no support ticket. Stripe's confirmation of the completed transfer is what finalizes the refund on our side; from there it posts to your original payment method within 5–10 business days, depending on your payment provider.

There is no dispute process. The build logs are the objective evidence — if the build fails, the refund triggers automatically.

---

## Build Logs

Your download archive includes a **`build-report.json`** at its root: the pipeline's own
record of every compiler invocation it ran against your code, written immediately before the
archive is zipped, so it describes exactly the code you received.

### When the build never passes

There is no archive, and therefore no `build-report.json`. The report is written on the
delivery path only — a build that exhausted its corrections is refunded, not packaged, so
there is no zip to put a report in and nothing to download. Every report that reaches you
describes code that compiled, which is why `status` is always `verified`.

The record of a failed generation is your **live build log**: every attempt, every command
and every compiler error is streamed to it as it happens, and it stays on the generation page
after the refund. That is the objective evidence referred to under Refund Conditions.

Field names are camelCase and stable within a `schemaVersion`.

### Top level

| Field | Type | Meaning |
|---|---|---|
| `schemaVersion` | number | Shape version of this file. Currently `1`. |
| `generationId` | string | Your generation's id — quote it in any support request. |
| `projectType` | string | Template family, e.g. `DotNetNextJs`. |
| `tier` | number | 1 Blueprint, 2 Boilerplate, 3 Infrastructure. |
| `generatedAt` | string | ISO-8601 UTC timestamp of delivery. |
| `status` | string | `verified`. See "when the build never passes" below — a report only exists in an archive, and a failed build produces no archive. |
| `attemptsUsed` | number | Build attempts that ran, including the successful one. |
| `maxAttempts` | number | The retry ceiling — `3`. |
| `halves` | array | Final verdict per half. See below. |
| `attempts` | array | Full history, oldest first. See below. |

### `halves[]` — what actually compiled

This is the honest answer to "is my code verified?", and it is per half, because the two
halves are verified by different toolchains and either can fail alone.

The two halves are the two halves of **your** stack:

| `projectType` | `half` values | `label` values |
|---|---|---|
| `DotNetNextJs` | `dotnet`, `nextjs` | `.NET`, `Next.js` |
| `PythonReact` | `python`, `react` | `FastAPI`, `React` |

| Field | Type | Meaning |
|---|---|---|
| `half` | string | Wire name of the half — see the table above. |
| `label` | string | Display name, as shown on your delivery page. |
| `status` | string | `passed`, `failed`, `skipped`, or `not_run`. |
| `commands` | string[] | The commands that produced this verdict, in order. |

Only `passed` is evidence of compilation. `skipped` means the step was reached and not
needed (no frontend in the archive, or no `typecheck` script). `not_run` means the half was
never reached, because the other half failed first.

Commands that were retried do not appear in `commands` and do not affect `status` — they are
in `attempts[].steps[]` with status `superseded`.

### `attempts[]` — the audit trail

| Field | Type | Meaning |
|---|---|---|
| `attempt` | number | 1-based, matching the `[Attempt N]` lines in your live build log. |
| `startedAt` / `completedAt` | string | ISO-8601 UTC. |
| `status` | string | `passed` or `failed`. |
| `steps` | array | Per-command detail — see below. |
| `output` | string | Full compiler output for the attempt, both halves, verbatim. Very long transcripts are truncated from the front; the tail (where the errors are) is always kept. |
| `errors` | string[] | The parsed error lines the correction prompt was built from. Empty on a passing attempt. |
| `correctedFiles` | string[] | Files the correction loop rewrote after this attempt failed. |

### `attempts[].steps[]` — per-command detail

| Field | Type | Meaning |
|---|---|---|
| `half` | string | Which half this command verified — see the table above. |
| `command` | string | The command as you would type it, e.g. `npm run build`. |
| `exitCode` | number | Process exit code. |
| `durationMs` | number | Wall-clock time for that command. |
| `errorCount` / `warningCount` | number | Diagnostics parsed out of that command's output. |
| `status` | string | `passed`, `failed`, `skipped`, or `superseded`. |

`superseded` means the command failed and a later command redid its work — `npm ci` failing
on a lockfile the correction loop changed, then succeeding as `npm install`. It is kept for
transparency; it is not a failure of your build, and it never decides a half's `status`.

### Example

```json
{
  "schemaVersion": 1,
  "generationId": "b3f1c8e2-…",
  "projectType": "DotNetNextJs",
  "tier": 2,
  "generatedAt": "2026-08-15T18:04:11.7420000+00:00",
  "status": "verified",
  "attemptsUsed": 1,
  "maxAttempts": 3,
  "halves": [
    { "half": "dotnet", "label": ".NET", "status": "passed",
      "commands": ["dotnet restore", "dotnet build --no-restore"] },
    { "half": "nextjs", "label": "Next.js", "status": "passed",
      "commands": ["npm ci", "npm run typecheck", "npm run build"] }
  ],
  "attempts": [
    {
      "attempt": 1,
      "startedAt": "2026-08-15T18:02:40.1120000+00:00",
      "completedAt": "2026-08-15T18:04:09.8830000+00:00",
      "status": "passed",
      "steps": [
        { "half": "dotnet", "command": "dotnet restore", "exitCode": 0,
          "durationMs": 8412, "errorCount": 0, "warningCount": 0, "status": "passed" },
        { "half": "nextjs", "command": "npm run build", "exitCode": 0,
          "durationMs": 41207, "errorCount": 0, "warningCount": 2, "status": "passed" }
      ],
      "output": "$ dotnet restore  (exit 0)\n…",
      "errors": [],
      "correctedFiles": []
    }
  ]
}
```

Tier 1 (Blueprint) archives contain no `build-report.json`: nothing in a Blueprint is
compiled, so there is no build to report. A Blueprint is not sold under the Compile
Guarantee.

The same verdict — `halves` plus `status` — is appended to your live build log inside a
`=== COMPILE GUARANTEE REPORT ===` block, which is what the delivery page reads to show
which halves compiled.

---

## Related Docs

- [The Swiss Cheese Method →](./swiss-cheese-method)
- [Tiers and Pricing →](../user/tiers-and-pricing)
- [Troubleshooting →](../user/troubleshooting)
