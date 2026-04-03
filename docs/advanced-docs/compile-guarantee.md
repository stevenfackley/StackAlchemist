# The Compile Guarantee

The Compile Guarantee is the technical promise at the center of StackAlchemist: every Boilerplate and Infrastructure package is verified to compile before delivery. This document explains exactly how it works.

---

## What It Is

For Tier 2 (Boilerplate) and Tier 3 (Infrastructure), before your archive is assembled and made available for download, the generated source code is physically run through the compiler:

- `dotnet build` for the .NET 10 Web API
- `npm run build` for the Next.js 15 frontend

If either build fails, the Compile Guarantee triggers an automatic correction loop. If the code still fails after the maximum number of retries, you receive a full refund — no dispute, no questions asked.

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

### Next.js Build (`npm run build`)

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

A refund is issued automatically when:

1. The generated code fails `dotnet build` or `npm run build`
2. The auto-correction loop has been executed 3 times
3. The build is still failing after all 3 correction attempts

The refund is processed to the original payment method within 5–10 business days, depending on your payment provider.

There is no dispute process. The build logs are the objective evidence — if the build fails, the refund triggers automatically.

---

## Build Logs

Your download archive includes a `build-report.json` file that records:
- Build attempt number
- Timestamp
- Full compiler output for each attempt
- Files that were corrected and the corrections applied
- Final build status

This provides full transparency into what the pipeline did.

---

## Related Docs

- [The Swiss Cheese Method →](./swiss-cheese-method)
- [Tiers and Pricing →](../user/tiers-and-pricing)
- [Troubleshooting →](../user/troubleshooting)
