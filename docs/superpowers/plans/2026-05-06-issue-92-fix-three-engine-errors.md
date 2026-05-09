# Issue #92 — Fix Three Engine Errors During Simple-Mode-Flow E2E Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate the three simultaneous backend errors observed in the nightly `simple-mode-flow.spec.ts` Playwright run (Anthropic 404, Supabase PATCH 400, R2 PUT 501) so the nightly suite produces meaningful signal again. Issue: https://github.com/stevenfackley/StackAlchemist/issues/92.

**Architecture:** Three independent root causes:

1. **Anthropic 404** — Engine defaults to `claude-3-5-sonnet-20241022` (retired by Anthropic). The CI workflow does not pass `ANTHROPIC_MODEL` through `setup-env`, so the engine container falls back to the dead default. Fix: bump default to `claude-sonnet-4-6`, wire `ANTHROPIC_MODEL` end-to-end (workflow var → setup-env → .env → engine).
2. **R2 PUT 501** — `AWSSDK.S3` was bumped 3.7→4.0 in PR #63 (DECISIONS.md, 2026-04-28). v4 emits checksum trailer headers (`x-amz-checksum-crc32`, streaming-trailer) by default; Cloudflare R2 returns 501 ("Not Implemented") for those. Fix: set `RequestChecksumCalculation = WHEN_REQUIRED` on the `AmazonS3Config`. Also add a HeadBucket fail-fast probe so future bucket misconfig surfaces with a clear `R2BucketNotFoundException` instead of the obscure upload error.
3. **Supabase PATCH 400** — The CI Supabase project (`cdlefpvsvyepofsboepc`, brand-new per memory) does not yet have migrations applied. Engine PATCHes against `/rest/v1/generations?id=eq.{id}` 400 because columns are missing or the `generations` table itself does not exist. Fix: add a CI step that runs `supabase db push --db-url $CI_SUPABASE_DB_URL` against the linked CI project before the E2E job spins up backend services.

**Tech Stack:** .NET 10 (Engine), AWSSDK.S3 v4, Cloudflare R2, GitHub Actions, Supabase CLI, Playwright, Next.js 15.

**Out of scope (follow-up issues):** general CI/CD audit (user wants this separately), reconciling docker-compose.prod.yml with the new model fallback chain beyond changing the default, BYOK encryption rotation.

---

## File Structure

### Modified
- `src/StackAlchemist.Engine/Services/CloudflareR2UploadService.cs` — checksum config + bucket probe
- `src/StackAlchemist.Engine/Services/AnthropicLlmClient.cs` — default model fallback
- `src/StackAlchemist.Engine/appsettings.json` — Anthropic model default
- `src/StackAlchemist.Engine/appsettings.Development.json` — Anthropic model default
- `src/StackAlchemist.Engine.Tests/Services/CloudflareR2UploadServiceTests.cs` — assert checksum config + bucket-probe error translation
- `src/StackAlchemist.Engine.Tests/Services/AnthropicLlmClientTests.cs` — update model assertions to 4.6
- `src/StackAlchemist.Engine.Tests/Services/SupabaseDeliveryServiceTests.cs` — model string in token-usage call
- `src/StackAlchemist.Engine.Tests/Services/InjectionEngineTests.cs` — model strings
- `src/StackAlchemist.Engine.Tests/Services/GenerationOrchestratorTests.cs` — model strings
- `src/StackAlchemist.Engine.Tests/Integration/MultiEcosystemPipelineTests.cs` — model string
- `src/StackAlchemist.Web/src/lib/actions.ts` — `DEFAULT_MODEL` + `ALLOWED_PROFILE_MODELS`
- `src/StackAlchemist.Web/src/app/dashboard/ByokSettingsForm.tsx` — `MODEL_OPTIONS` default value
- `src/StackAlchemist.Web/__tests__/mocks/handlers.ts` — `preferredModel` default in mock
- `.github/actions/setup-env/action.yml` — accept `anthropic_model` input, write to `.env`
- `.github/workflows/ci.yml` — pass `vars.ANTHROPIC_MODEL`; new "Apply Supabase migrations" step in `e2e-integration`
- `.github/workflows/deploy-test.yml` — pass `vars.ANTHROPIC_MODEL`
- `.github/workflows/deploy-prod.yml` — pass `vars.ANTHROPIC_MODEL` (if it uses setup-env)
- `docker-compose.prod.yml` — fallback model `4-5-20250929` → `4-6`
- `.env.example` — add `ANTHROPIC_MODEL` line
- `CLAUDE.md` (project root) — replace "Claude 3.5 Sonnet" mandate with "Claude Sonnet 4.6"
- `docs/DECISIONS.md` — new ADR entry for the model bump
- `docs/advanced-docs/self-hosting.md` — update `ANTHROPIC_MODEL` example

### Created
- `src/StackAlchemist.Engine/Services/R2BucketException.cs` — typed exceptions for bucket existence/access errors
- `supabase/migrations/20260506000008_update_profile_model_default_to_sonnet_4_6.sql` — bump column default
- `docs/runbooks/ci-supabase-migrations.md` — runbook for applying migrations to the CI project, including the new `CI_SUPABASE_DB_URL` secret

### Conventions to follow
- Each phase produces one logical commit; user prefers bundled work in a single PR for related fixes (memory: `feedback_content_strategy` / bundled-PR preference). Final PR opens against `main` with three commits.
- Conventional commits: `fix(engine):`, `chore(model):`, `ci(supabase):`.
- TDD: write the failing test first, run it, implement, run again, commit.

---

## Phase A — R2 hardening (Error #3)

### Task A1: Add `RequestChecksumCalculation = WHEN_REQUIRED` to R2 S3 config

**Why this matters:** This is the root cause of the 501. AWSSDK v4 sends `x-amz-checksum-crc32` and chunked-encoding trailer headers by default. R2 has not implemented those flavors and rejects with 501. Setting `WHEN_REQUIRED` reverts to v3-style behavior (no checksum unless the API explicitly demands one), which R2 accepts.

**Files:**
- Modify: `src/StackAlchemist.Engine/Services/CloudflareR2UploadService.cs:42-49`
- Test: `src/StackAlchemist.Engine.Tests/Services/CloudflareR2UploadServiceTests.cs`

- [ ] **Step 1: Write the failing test for checksum config**

The current `BuildSut` returns the service directly. To assert checksum config without an actual R2 round-trip, refactor `CloudflareR2UploadService` to expose its `AmazonS3Config` factory as an internal helper, then unit-test it.

Add to `CloudflareR2UploadServiceTests.cs`:

```csharp
[Fact]
public void BuildS3Config_SetsRequestChecksumCalculationToWhenRequired()
{
    var sut = BuildSut(BuildConfig());

    var s3Config = CloudflareR2UploadService.BuildS3ConfigForTests(
        accountId: "test-account");

    s3Config.RequestChecksumCalculation
        .Should().Be(Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
            because: "R2 returns 501 for AWSSDK v4 default trailer-checksum headers (issue #92)");
    s3Config.ResponseChecksumValidation
        .Should().Be(Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED,
            because: "R2 does not echo x-amz-checksum-* response headers");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/StackAlchemist.Engine.Tests/ --filter "BuildS3Config_SetsRequestChecksumCalculationToWhenRequired"`
Expected: FAIL — `BuildS3ConfigForTests` does not exist; or, once added without the property, returns the default `WHEN_SUPPORTED` value.

- [ ] **Step 3: Refactor `CloudflareR2UploadService` to expose the config builder**

Replace the inline `var s3Config = new AmazonS3Config { ... }` block in `UploadZipAsync` with a call to a new private helper, and add an `internal static` test surface:

```csharp
// Inside CloudflareR2UploadService.cs

internal static AmazonS3Config BuildS3ConfigForTests(string accountId) =>
    BuildS3Config(accountId);

private static AmazonS3Config BuildS3Config(string accountId) =>
    new()
    {
        ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
        ForcePathStyle = true,
        AuthenticationRegion = "auto",
        // R2 returns 501 for AWSSDK v4's default checksum trailers
        // (x-amz-checksum-crc32 + STREAMING-UNSIGNED-PAYLOAD-TRAILER).
        // WHEN_REQUIRED matches the v3 behavior that worked.
        RequestChecksumCalculation  = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation  = ResponseChecksumValidation.WHEN_REQUIRED,
    };
```

Then in `UploadZipAsync`, replace the existing config block with `var s3Config = BuildS3Config(accountId);`. Keep the existing `using var s3 = new AmazonS3Client(credentials, s3Config);` line.

The `internal static` is reachable from tests because the project already does `InternalsVisibleTo`. If it does not (check `.csproj`/AssemblyInfo), add `[assembly: InternalsVisibleTo("StackAlchemist.Engine.Tests")]` in a new `src/StackAlchemist.Engine/AssemblyInfo.cs`. **Verify before adding** — running `grep -rn "InternalsVisibleTo" src/StackAlchemist.Engine` first; if already present, do nothing.

Also add `using Amazon.Runtime;` at the top of `CloudflareR2UploadService.cs` if not already present (the `RequestChecksumCalculation`/`ResponseChecksumValidation` enums live there).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/StackAlchemist.Engine.Tests/ --filter "BuildS3Config_SetsRequestChecksumCalculationToWhenRequired"`
Expected: PASS.

Then run the full R2 test class to confirm no regressions:

Run: `dotnet test src/StackAlchemist.Engine.Tests/ --filter "FullyQualifiedName~CloudflareR2UploadServiceTests"`
Expected: all 4 existing tests + the new one PASS. The skipped integration test stays skipped.

- [ ] **Step 5: Do not commit yet — Task A2 lands in the same commit**

This task and Task A2 produce one commit (`fix(engine): R2 — disable v4 checksum trailers and harden bucket existence`). Move to A2.

---

### Task A2: Fail-fast bucket existence probe with translated errors

**Why this matters:** Today, if `R2_BUCKET_NAME` is wrong or the bucket was never created in the right Cloudflare account, the user sees a generic `AmazonS3Exception` deep in the upload path — after we have already zipped the project into memory. We want to: (a) detect the misconfig before doing work, (b) translate to a typed exception with a clear message that names the offending env var.

**Files:**
- Create: `src/StackAlchemist.Engine/Services/R2BucketException.cs`
- Modify: `src/StackAlchemist.Engine/Services/CloudflareR2UploadService.cs`
- Test: `src/StackAlchemist.Engine.Tests/Services/CloudflareR2UploadServiceTests.cs`

- [ ] **Step 1: Create the exception types**

Write `src/StackAlchemist.Engine/Services/R2BucketException.cs`:

```csharp
namespace StackAlchemist.Engine.Services;

/// <summary>
/// Base type for Cloudflare R2 bucket-configuration failures.
/// Throw when the bucket cannot be reached due to a mis-set
/// R2_BUCKET_NAME / R2_ACCOUNT_ID / credentials, distinct from
/// transient network or upload errors.
/// </summary>
public abstract class R2BucketException(string message) : InvalidOperationException(message);

public sealed class R2BucketNotFoundException(string bucket, string accountId)
    : R2BucketException(
        $"R2 bucket '{bucket}' was not found in account '{accountId}'. " +
        $"Check R2_BUCKET_NAME and R2_ACCOUNT_ID, and verify the bucket exists in the Cloudflare R2 dashboard.");

public sealed class R2BucketAccessDeniedException(string bucket)
    : R2BucketException(
        $"R2 bucket '{bucket}' exists but the configured credentials cannot access it. " +
        $"Check R2_ACCESS_KEY_ID / R2_SECRET_ACCESS_KEY scopes (must include Object Read & Write for this bucket).");
```

- [ ] **Step 2: Write failing tests for error translation**

Append to `CloudflareR2UploadServiceTests.cs`:

```csharp
[Fact]
public void TranslateBucketProbeError_NoSuchBucketStatus_ReturnsR2BucketNotFoundException()
{
    var ex = CloudflareR2UploadService.TranslateBucketProbeErrorForTests(
        statusCode: System.Net.HttpStatusCode.NotFound,
        errorCode: "NoSuchBucket",
        bucket: "stackalchemist-generations-test",
        accountId: "abc123");

    ex.Should().BeOfType<R2BucketNotFoundException>();
    ex.Message.Should().Contain("stackalchemist-generations-test");
    ex.Message.Should().Contain("R2_BUCKET_NAME");
    ex.Message.Should().Contain("abc123");
}

[Fact]
public void TranslateBucketProbeError_ForbiddenStatus_ReturnsR2BucketAccessDeniedException()
{
    var ex = CloudflareR2UploadService.TranslateBucketProbeErrorForTests(
        statusCode: System.Net.HttpStatusCode.Forbidden,
        errorCode: "AccessDenied",
        bucket: "stackalchemist-generations-test",
        accountId: "abc123");

    ex.Should().BeOfType<R2BucketAccessDeniedException>();
    ex.Message.Should().Contain("R2_ACCESS_KEY_ID");
}

[Fact]
public void TranslateBucketProbeError_OtherStatus_ReturnsNullSoCallerCanRethrowOriginal()
{
    var ex = CloudflareR2UploadService.TranslateBucketProbeErrorForTests(
        statusCode: System.Net.HttpStatusCode.InternalServerError,
        errorCode: "InternalError",
        bucket: "any",
        accountId: "any");

    ex.Should().BeNull(because: "transient 5xx errors should bubble up unchanged so retry logic can act on them");
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test src/StackAlchemist.Engine.Tests/ --filter "TranslateBucketProbeError"`
Expected: FAIL — `TranslateBucketProbeErrorForTests` does not exist.

- [ ] **Step 4: Implement the bucket existence probe + error translation in `CloudflareR2UploadService`**

Edit `src/StackAlchemist.Engine/Services/CloudflareR2UploadService.cs`:

a) Add a private static `TranslateBucketProbeError` and an `internal static` test surface:

```csharp
internal static R2BucketException? TranslateBucketProbeErrorForTests(
    System.Net.HttpStatusCode statusCode,
    string? errorCode,
    string bucket,
    string accountId) =>
    TranslateBucketProbeError(statusCode, errorCode, bucket, accountId);

private static R2BucketException? TranslateBucketProbeError(
    System.Net.HttpStatusCode statusCode,
    string? errorCode,
    string bucket,
    string accountId)
{
    if (statusCode == System.Net.HttpStatusCode.NotFound ||
        string.Equals(errorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase))
    {
        return new R2BucketNotFoundException(bucket, accountId);
    }

    if (statusCode == System.Net.HttpStatusCode.Forbidden ||
        string.Equals(errorCode, "AccessDenied", StringComparison.OrdinalIgnoreCase))
    {
        return new R2BucketAccessDeniedException(bucket);
    }

    return null; // unknown — let the caller bubble the original exception
}
```

b) Inside `UploadZipAsync`, before zipping, call a new probe method. Replace the current "1. Zip the directory" comment block with this ordering:

```csharp
// ── 1. Verify bucket is reachable (fail-fast on misconfig) ───────────
var credentials = new BasicAWSCredentials(accessKey, secretKey);
var s3Config = BuildS3Config(accountId);
using var s3 = new AmazonS3Client(credentials, s3Config);

await EnsureBucketExistsAsync(s3, bucket, accountId, ct);

// ── 2. Zip the directory into an in-memory stream ─────────────────────
using var memStream = new MemoryStream();
ZipFile.CreateFromDirectory(directoryPath, memStream);
memStream.Position = 0;

LogUploadingZip(logger, generationId, memStream.Length, bucket, key);

// ── 3. Upload via AWS SDK (R2 is S3-compatible) ───────────────────────
var putRequest = new PutObjectRequest
{
    BucketName = bucket,
    Key = key,
    InputStream = memStream,
    ContentType = "application/zip",
};
await s3.PutObjectAsync(putRequest, ct);
```

(Note the inversion: bucket probe first, then zip — saves the in-memory zip cost when the bucket is misconfigured.)

c) Add the probe method:

```csharp
private async Task EnsureBucketExistsAsync(
    AmazonS3Client s3,
    string bucket,
    string accountId,
    CancellationToken ct)
{
    try
    {
        await s3.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = bucket }, ct);
    }
    catch (AmazonS3Exception ex)
    {
        var translated = TranslateBucketProbeError(ex.StatusCode, ex.ErrorCode, bucket, accountId);
        if (translated is not null)
        {
            LogR2BucketProbeFailed(logger, bucket, accountId, ex.ErrorCode ?? "(none)", (int)ex.StatusCode);
            throw translated;
        }
        throw;
    }
}
```

d) Add the LoggerMessage source-gen entry near the existing ones at the bottom of the file:

```csharp
[LoggerMessage(EventId = 902, Level = LogLevel.Error, Message = "R2 bucket probe failed: bucket={Bucket}, account={Account}, errorCode={ErrorCode}, status={Status}")]
private static partial void LogR2BucketProbeFailed(ILogger logger, string bucket, string account, string errorCode, int status);
```

(`GetBucketLocationAsync` is the standard AWSSDK call for bucket existence + access — `HeadBucketAsync` also works but `GetBucketLocation` returns 404 for missing and 403 for no-access without consuming object-list permissions.)

- [ ] **Step 5: Run all R2 tests**

Run: `dotnet test src/StackAlchemist.Engine.Tests/ --filter "FullyQualifiedName~CloudflareR2UploadServiceTests"`
Expected: 7 tests pass (3 existing config-validation + 1 new checksum config + 3 new translate-error). The skipped integration test stays skipped.

- [ ] **Step 6: Build the engine and confirm clean compile**

Run: `dotnet build src/StackAlchemist.Engine/ --configuration Release`
Expected: build succeeds, no warnings about unused imports.

- [ ] **Step 7: Commit Phase A**

```bash
git add src/StackAlchemist.Engine/Services/CloudflareR2UploadService.cs \
        src/StackAlchemist.Engine/Services/R2BucketException.cs \
        src/StackAlchemist.Engine.Tests/Services/CloudflareR2UploadServiceTests.cs
# If you added InternalsVisibleTo:
# git add src/StackAlchemist.Engine/AssemblyInfo.cs
git commit -m "$(cat <<'EOF'
fix(engine): R2 — disable v4 checksum trailers and harden bucket existence

AWSSDK.S3 4.x emits x-amz-checksum-crc32 + STREAMING-UNSIGNED-PAYLOAD-TRAILER
headers by default. Cloudflare R2 returns 501 Not Implemented for those, which
surfaced as the third nightly E2E error in issue #92.

- Set RequestChecksumCalculation/ResponseChecksumValidation = WHEN_REQUIRED
  on AmazonS3Config (matches the v3 behavior that worked).
- Add a fail-fast GetBucketLocation probe before zipping. Translates 404/
  NoSuchBucket → R2BucketNotFoundException and 403/AccessDenied →
  R2BucketAccessDeniedException so future misconfig surfaces with a clear
  message that names the offending env var instead of a generic upload error.

Refs #92.
EOF
)"
```

---

## Phase B — Anthropic Sonnet 4.6 bump (Error #1)

### Task B1: Bump engine default model to `claude-sonnet-4-6`

**Why this matters:** The 404 from `api.anthropic.com` happens because the engine sends `claude-3-5-sonnet-20241022` — Anthropic retired that model. We bump the engine default and all test fixtures to `claude-sonnet-4-6` so a misconfigured deploy lands on a current model rather than a dead one.

**Files:**
- Modify: `src/StackAlchemist.Engine/Services/AnthropicLlmClient.cs:32`
- Modify: `src/StackAlchemist.Engine/appsettings.json:10`
- Modify: `src/StackAlchemist.Engine/appsettings.Development.json:11`
- Modify: `src/StackAlchemist.Engine.Tests/Services/AnthropicLlmClientTests.cs:25, 74`
- Modify: `src/StackAlchemist.Engine.Tests/Services/SupabaseDeliveryServiceTests.cs:145`
- Modify: `src/StackAlchemist.Engine.Tests/Services/InjectionEngineTests.cs:285, 292`
- Modify: `src/StackAlchemist.Engine.Tests/Services/GenerationOrchestratorTests.cs:57, 146, 281, 320`
- Modify: `src/StackAlchemist.Engine.Tests/Integration/MultiEcosystemPipelineTests.cs:47`

- [ ] **Step 1: Update the engine fallback constant**

Edit `src/StackAlchemist.Engine/Services/AnthropicLlmClient.cs` line 32:

```csharp
var model = config["Anthropic:Model"] ?? "claude-sonnet-4-6";
```

- [ ] **Step 2: Update appsettings defaults**

Edit `src/StackAlchemist.Engine/appsettings.json` line 10:

```json
"Anthropic": {
  "Model": "claude-sonnet-4-6",
  "MaxTokens": "8192"
},
```

Edit `src/StackAlchemist.Engine/appsettings.Development.json` line 11 with the same change.

- [ ] **Step 3: Update all test fixtures**

Use a single replace across the Engine.Tests project. Run from repo root:

```bash
grep -rln "claude-3-5-sonnet-20241022" src/StackAlchemist.Engine.Tests/
```

Then for each file in the list, edit replacing `claude-3-5-sonnet-20241022` with `claude-sonnet-4-6`. Specifically expected files:

- `AnthropicLlmClientTests.cs` (lines 25, 74)
- `SupabaseDeliveryServiceTests.cs` (line 145)
- `InjectionEngineTests.cs` (lines 285, 292)
- `GenerationOrchestratorTests.cs` (lines 57, 146, 281, 320)
- `MultiEcosystemPipelineTests.cs` (line 47)

Use `Edit` with `replace_all` per file, not a global script — keeps the diff reviewable.

- [ ] **Step 4: Build and run engine tests**

Run: `dotnet test src/StackAlchemist.Engine.Tests/ --configuration Release`
Expected: all tests PASS. Look specifically for `GenerateAsync_ValidResponse_ReturnsTextContent` and `UpdateTokenUsageAsync` — those assert the model string value flows through.

- [ ] **Step 5: Do not commit yet — B2 lands in the same commit**

---

### Task B2: Update Web `DEFAULT_MODEL`, ByokSettingsForm options, mock, and Supabase migration

**Why this matters:** The Next.js side has its own `DEFAULT_MODEL` and an allowlist that the BYOK form draws from. If we leave it pointing at the dead 3.5 ID, the form keeps offering it (and writes it to profiles.preferred_model). The mock in `__tests__/mocks/handlers.ts` is the source of truth for vitest — also stale. The Supabase profiles default similarly needs to advance.

**Files:**
- Modify: `src/StackAlchemist.Web/src/lib/actions.ts:25-31`
- Modify: `src/StackAlchemist.Web/src/app/dashboard/ByokSettingsForm.tsx:13-30`
- Modify: `src/StackAlchemist.Web/__tests__/mocks/handlers.ts:66`
- Create: `supabase/migrations/20260506000008_update_profile_model_default_to_sonnet_4_6.sql`

- [ ] **Step 1: Update `DEFAULT_MODEL` and `ALLOWED_PROFILE_MODELS` in actions.ts**

Edit `src/StackAlchemist.Web/src/lib/actions.ts` lines 25-31:

```ts
const DEFAULT_MODEL = "claude-sonnet-4-6";
const ALLOWED_PROFILE_MODELS = new Set([
  DEFAULT_MODEL,
  "claude-3-5-sonnet-20241022",  // legacy — still allowed via BYOK
  "claude-3-5-haiku-20241022",
  "openai/gpt-4o-mini",
  "openrouter/anthropic/claude-3.5-sonnet",
]);
```

(Reasoning for keeping the old IDs in the allowlist: existing user profiles in prod still hold those values; we don't want `normalizePreferredModel` to silently rewrite them to default the next time a profile is read. Adding a "legacy" comment makes the intent explicit. Removal can be a separate migration once we've checked production data.)

- [ ] **Step 2: Update `MODEL_OPTIONS` in ByokSettingsForm.tsx**

Edit `src/StackAlchemist.Web/src/app/dashboard/ByokSettingsForm.tsx`:

Replace the `MODEL_OPTIONS` array starting at line 13 with:

```tsx
const MODEL_OPTIONS = [
  {
    value: "claude-sonnet-4-6",
    label: "Claude Sonnet 4.6 (default)",
  },
  {
    value: "claude-3-5-sonnet-20241022",
    label: "Claude 3.5 Sonnet (legacy)",
  },
  {
    value: "claude-3-5-haiku-20241022",
    label: "Claude 3.5 Haiku",
  },
  {
    value: "openai/gpt-4o-mini",
    label: "OpenAI GPT-4o mini (BYOK)",
  },
  {
    value: "openrouter/anthropic/claude-3.5-sonnet",
    label: "OpenRouter Claude 3.5 Sonnet (BYOK)",
  },
];
```

- [ ] **Step 3: Update the BYOK mock handler**

Edit `src/StackAlchemist.Web/__tests__/mocks/handlers.ts` line 66, change the `preferredModel` value:

```ts
return HttpResponse.json({ email: 'test@example.com', hasApiKeyOverride: false, preferredModel: 'claude-sonnet-4-6' });
```

- [ ] **Step 4: Create new Supabase migration**

Create `supabase/migrations/20260506000008_update_profile_model_default_to_sonnet_4_6.sql`:

```sql
-- Bump the column-level default for profiles.preferred_model from
-- the retired claude-3-5-sonnet-20241022 to claude-sonnet-4-6.
-- Existing rows are left untouched: a user who explicitly chose 3.5
-- (or had it written before they reviewed BYOK options) keeps their
-- choice. The legacy value is still in ALLOWED_PROFILE_MODELS so
-- normalizePreferredModel does not silently rewrite it.

alter table public.profiles
  alter column preferred_model set default 'claude-sonnet-4-6';
```

- [ ] **Step 5: Run frontend test suite**

```bash
cd src/StackAlchemist.Web
npx vitest run
```

Expected: all tests PASS. The mock-handler change touches BYOK tests if any reference `preferredModel`; check the Vitest report for any handler-related failures and update test expectations to match the new default if needed.

- [ ] **Step 6: Lint and typecheck**

```bash
cd src/StackAlchemist.Web
npm run lint
npx tsc --noEmit
```

Expected: clean.

- [ ] **Step 7: Do not commit yet — B3 and B4 land in the same commit**

---

### Task B3: Wire `ANTHROPIC_MODEL` end-to-end through CI/deploy workflows

**Why this matters:** The reason the engine fell back to its default in CI is that nothing on the workflow path passes `ANTHROPIC_MODEL`. With the default fixed, CI will at least no longer 404. But making the env var a first-class workflow input keeps the model under explicit configuration control: when Anthropic next deprecates a model, we update one repo variable instead of editing source.

**Files:**
- Modify: `.github/actions/setup-env/action.yml`
- Modify: `.github/workflows/ci.yml`
- Modify: `.github/workflows/deploy-test.yml`
- Modify: `.github/workflows/deploy-prod.yml`
- Modify: `.env.example`
- Modify: `docker-compose.prod.yml`

- [ ] **Step 1: Add `anthropic_model` input to `setup-env` action**

Edit `.github/actions/setup-env/action.yml`. In the `inputs:` block, after `anthropic_api_key` (line ~89), add:

```yaml
  anthropic_model:
    description: 'Anthropic model id (e.g. claude-sonnet-4-6). Empty → engine default applies.'
    required: false
    default: ''
```

In the `env:` block of the run step (around line 154 after `ANTHROPIC_API_KEY`), add:

```yaml
        ANTHROPIC_MODEL: ${{ inputs.anthropic_model }}
```

In the heredoc that writes `.env` (after the `ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}` line, line ~199), add:

```bash
        ANTHROPIC_MODEL=${ANTHROPIC_MODEL}
```

- [ ] **Step 2: Pass `vars.ANTHROPIC_MODEL` from ci.yml**

Edit `.github/workflows/ci.yml` in the `e2e-integration` job's `Setup .env (test)` step (around line 261). After the `anthropic_api_key:` line (line 281) add:

```yaml
          anthropic_model: ${{ vars.ANTHROPIC_MODEL || 'claude-sonnet-4-6' }}
```

(Why `vars` not `secrets`: a model id is not sensitive. `vars.ANTHROPIC_MODEL` is settable per environment. The `|| 'claude-sonnet-4-6'` fallback means CI still works before the var is configured.)

- [ ] **Step 3: Pass `vars.ANTHROPIC_MODEL` from deploy-test.yml**

Edit `.github/workflows/deploy-test.yml` line ~54 after the `anthropic_api_key:` line:

```yaml
          anthropic_model: ${{ vars.ANTHROPIC_MODEL || 'claude-sonnet-4-6' }}
```

- [ ] **Step 4: Pass `vars.ANTHROPIC_MODEL` from deploy-prod.yml (if applicable)**

Read `.github/workflows/deploy-prod.yml` first. If it uses the `setup-env` composite action, add the same `anthropic_model:` line as B3-Step3. If it does NOT use setup-env (e.g., it sets env vars manually in compose env), set `ANTHROPIC_MODEL` explicitly via the workflow `env:` or compose override.

```bash
grep -n "setup-env\|anthropic_api_key\|ANTHROPIC_MODEL" .github/workflows/deploy-prod.yml
```

Use the output to decide where to inject. Make the prod default `claude-sonnet-4-6` to match the test default — both should track the same model unless prod is intentionally pinned.

- [ ] **Step 5: Update `.env.example`**

Edit `.env.example`. After the `ANTHROPIC_API_KEY=...` block (line 58), add:

```
# Anthropic model id. When empty, the engine falls back to claude-sonnet-4-6.
# Bump this value when Anthropic deprecates the current model.
ANTHROPIC_MODEL=claude-sonnet-4-6
```

- [ ] **Step 6: Update `docker-compose.prod.yml` fallback**

Edit `docker-compose.prod.yml` line 86:

```yaml
      ANTHROPIC_MODEL: ${ANTHROPIC_MODEL:-claude-sonnet-4-6}
```

(So compose-only deploys without the workflow vars still land on a current model.)

- [ ] **Step 7: Validate workflows parse**

```bash
# Use the GitHub CLI to check YAML parses on a dry-run by listing workflows
gh workflow list --repo stevenfackley/StackAlchemist
```

If `gh actions-yaml` lint is available (`gh extension list`), run it; otherwise rely on the next step.

- [ ] **Step 8: Do not commit yet — B4 lands in the same commit**

---

### Task B4: Update CLAUDE.md, DECISIONS.md, and self-hosting docs

**Why this matters:** The project `CLAUDE.md` mandates "Claude 3.5 Sonnet API" for margin reasons. That mandate is now stale (prod has been on Sonnet 4.5 since the 2026-04 switch; we're moving to 4.6 here). Future agents need the current rule, not the old one.

**Files:**
- Modify: `CLAUDE.md` (project root, NOT the `~/.claude/CLAUDE.md` global one)
- Modify: `docs/DECISIONS.md`
- Modify: `docs/advanced-docs/self-hosting.md`

- [ ] **Step 1: Update project `CLAUDE.md`**

Edit `C:\Users\steve\projects\StackAlchemist\CLAUDE.md`. Locate the section "⚠️ CRITICAL DISTINCTION: THE BUILDER VS. THE PRODUCT" → "The Product" bullet (currently mentions "Claude 3.5 Sonnet API") and replace with:

```markdown
- **The Product (StackAlchemist):** The SaaS platform you are building. When you write the backend code that makes LLM API calls for our users, **the application must use the Claude Sonnet 4.6 API** (`claude-sonnet-4-6`) or the user's BYOK key. The current model is set via the `ANTHROPIC_MODEL` env var (workflow-controlled in CI/deploy via `vars.ANTHROPIC_MODEL`). Watch Anthropic's deprecation schedule and bump this when needed; if a price drop lands on a cheaper Sonnet variant we may revisit, but at equal price, use the latest.
```

Also remove the old "🏗️ Technical Constraints (For the App You Are Building)" → "Core Engine" line if it references "Claude 3.5 generated business logic" — replace `Claude 3.5` with `Claude Sonnet 4.6`. (Use `grep -n "Claude 3.5" CLAUDE.md` to find every occurrence.)

- [ ] **Step 2: Add a DECISIONS.md ADR entry**

Append to `docs/DECISIONS.md` (and the root `DECISIONS.md` mirror, if it exists — check first):

```markdown

## 2026-05-06 — Bump Anthropic default to claude-sonnet-4-6

**Status:** accepted
**Context:** Issue #92 — engine returns 404 from api.anthropic.com because
`claude-3-5-sonnet-20241022` was retired. Prod compose was on
`claude-sonnet-4-5-20250929`; CI was falling back to the dead engine default
because `ANTHROPIC_MODEL` was not threaded through the workflow.
**Decision:**
- Engine default → `claude-sonnet-4-6` (in code, appsettings, all test fixtures).
- Web `DEFAULT_MODEL` → `claude-sonnet-4-6`. Old IDs stay in
  `ALLOWED_PROFILE_MODELS` so existing profiles aren't silently rewritten.
- Wire `ANTHROPIC_MODEL` through `setup-env` action and all workflows; resolve
  from `vars.ANTHROPIC_MODEL` (repo/environment variable, not secret) with
  `'claude-sonnet-4-6'` as the workflow-level fallback.
- New Supabase migration bumps `profiles.preferred_model` column default.
**Consequences:**
- One env var (`ANTHROPIC_MODEL`) is now the single point of control for the
  active model — future deprecations require flipping one variable rather
  than editing source.
- Pricing parity with 4.5 means no cost regression. Watch Anthropic's
  deprecation page; downgrade only if 4.5 drops in price.
- BYOK paths still allow legacy 3.5 IDs for users who explicitly chose them.
```

- [ ] **Step 3: Update self-hosting docs**

Edit `docs/advanced-docs/self-hosting.md` line 90-91:

```
# Override Claude model (default: claude-sonnet-4-6)
ANTHROPIC_MODEL=claude-sonnet-4-6
```

- [ ] **Step 4: Run frontend tests one more time + engine tests**

```bash
dotnet test src/StackAlchemist.Engine.Tests/ --configuration Release
cd src/StackAlchemist.Web && npx vitest run && npx tsc --noEmit
```

Expected: all green.

- [ ] **Step 5: Commit Phase B**

```bash
git add src/StackAlchemist.Engine/Services/AnthropicLlmClient.cs \
        src/StackAlchemist.Engine/appsettings.json \
        src/StackAlchemist.Engine/appsettings.Development.json \
        src/StackAlchemist.Engine.Tests/ \
        src/StackAlchemist.Web/src/lib/actions.ts \
        src/StackAlchemist.Web/src/app/dashboard/ByokSettingsForm.tsx \
        src/StackAlchemist.Web/__tests__/mocks/handlers.ts \
        supabase/migrations/20260506000008_update_profile_model_default_to_sonnet_4_6.sql \
        .github/actions/setup-env/action.yml \
        .github/workflows/ci.yml \
        .github/workflows/deploy-test.yml \
        .github/workflows/deploy-prod.yml \
        .env.example \
        docker-compose.prod.yml \
        CLAUDE.md \
        docs/DECISIONS.md \
        docs/advanced-docs/self-hosting.md

git commit -m "$(cat <<'EOF'
chore(model): bump Anthropic default to claude-sonnet-4-6

Anthropic retired claude-3-5-sonnet-20241022; the nightly E2E was 404'ing
because the engine fell back to it (issue #92, error #1). Bumping the engine
default + wiring ANTHROPIC_MODEL through the workflow path so future
deprecations are one repo-variable change.

- Engine: AnthropicLlmClient fallback + appsettings → claude-sonnet-4-6.
- Web: DEFAULT_MODEL + ByokSettingsForm options bumped; legacy IDs kept in
  ALLOWED_PROFILE_MODELS so existing profiles aren't silently rewritten.
- New supabase migration bumps profiles.preferred_model default.
- setup-env action accepts anthropic_model input; ci.yml + deploy-test.yml +
  deploy-prod.yml resolve from vars.ANTHROPIC_MODEL with a 4.6 fallback.
- docker-compose.prod.yml fallback updated.
- CLAUDE.md product mandate updated; DECISIONS.md ADR added.

Refs #92.
EOF
)"
```

---

## Phase C — Apply Supabase migrations to CI project (Error #2)

### Task C1: Add "Apply Supabase migrations" step to e2e-integration job

**Why this matters:** The CI Supabase project (`cdlefpvsvyepofsboepc`) is brand-new and has no schema yet. The Engine PATCH 400s because the `generations` table either doesn't exist or is missing columns. Running `supabase db push` is idempotent (uses the `supabase_migrations.schema_migrations` tracking table), so this step is safe to run on every CI run — first run applies all 9 migrations, subsequent runs apply only new ones.

**Files:**
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: Add the migration step to `e2e-integration`**

Edit `.github/workflows/ci.yml`. The new step goes between `Validate Required Integration Secrets` (ends at line 391) and `Copy .env to Next.js project` (starts at line 393). Insert:

```yaml
      - name: Apply Supabase migrations to CI project
        if: steps.validate-secrets.outputs.skip_e2e != 'true'
        env:
          # Postgres connection string for the CI Supabase project
          # (project ref cdlefpvsvyepofsboepc). Format:
          # postgres://postgres.<ref>:<password>@aws-0-<region>.pooler.supabase.com:5432/postgres
          # See docs/runbooks/ci-supabase-migrations.md.
          SUPABASE_DB_URL: ${{ secrets.CI_SUPABASE_DB_URL }}
        run: |
          set -euo pipefail

          if [ -z "${SUPABASE_DB_URL:-}" ]; then
            echo "::error::CI_SUPABASE_DB_URL secret is not set. See docs/runbooks/ci-supabase-migrations.md."
            exit 1
          fi

          # Mask the value defensively (GitHub auto-masks secrets but a
          # connection string contains a password we don't want leaking
          # via any derived form).
          echo "::add-mask::${SUPABASE_DB_URL}"

          # Install the Supabase CLI (pinned to a known-good major).
          curl -fsSL https://github.com/supabase/cli/releases/latest/download/supabase_linux_amd64.tar.gz \
            | tar -xz -C /tmp
          sudo mv /tmp/supabase /usr/local/bin/supabase
          supabase --version

          # Apply migrations. Idempotent: the CLI tracks applied migrations
          # in supabase_migrations.schema_migrations.
          supabase db push \
            --db-url "$SUPABASE_DB_URL" \
            --include-all
```

- [ ] **Step 2: Verify Supabase CLI download URL is current**

The latest-release URL pattern (`releases/latest/download/supabase_linux_amd64.tar.gz`) has been stable, but verify before committing:

```bash
curl -sIL https://github.com/supabase/cli/releases/latest/download/supabase_linux_amd64.tar.gz | head -5
```

Expected: `HTTP/2 302` redirect to a versioned tarball, ending in 200. If not, switch to a pinned version (e.g. `releases/download/v1.250.4/supabase_linux_amd64.tar.gz`) and add a TODO to bump quarterly.

- [ ] **Step 3: Verify the workflow YAML parses**

```bash
gh workflow view ci.yml --repo stevenfackley/StackAlchemist >/dev/null && echo "current view OK"
# Once committed and pushed, gh workflow lint runs server-side on push;
# the next CI run will tell us if the new step parses.
```

- [ ] **Step 4: Do not commit yet — C2 lands in the same commit**

---

### Task C2: Add runbook documentation for the new secret

**Why this matters:** `CI_SUPABASE_DB_URL` is a new secret. The user needs a single document that explains: where to get the value, which GitHub environment to add it to, and how to rotate it. This also serves as future-you's reference when the secret needs renewal.

**Files:**
- Create: `docs/runbooks/ci-supabase-migrations.md`

- [ ] **Step 1: Verify runbook directory exists**

```bash
ls docs/runbooks/ 2>/dev/null || mkdir -p docs/runbooks
```

- [ ] **Step 2: Write the runbook**

Create `docs/runbooks/ci-supabase-migrations.md`:

```markdown
# CI Supabase Migrations Runbook

The E2E Integration job (`.github/workflows/ci.yml`, job `e2e-integration`)
applies `supabase/migrations/*.sql` to the CI-only Supabase project on every
run before the engine starts. This document covers the secret it depends on
and the failure modes you'll see.

## Secret: `CI_SUPABASE_DB_URL`

A Postgres connection string for the CI-only Supabase project (ref:
`cdlefpvsvyepofsboepc`). Stored in the **Test** GitHub environment so the
`e2e-integration` job (`environment: test`) sees it but other jobs do not.

### How to get the value

1. Open the Supabase dashboard for project `cdlefpvsvyepofsboepc`.
2. **Project Settings → Database → Connection string → URI tab**.
3. Copy the **Transaction Pooler** URL (port 6543) for the CLI — it survives
   IP rotation and Supabase restarts. The Direct Connection URL works too
   but is less robust on shared runners.
4. Replace `[YOUR-PASSWORD]` with the database password set when the project
   was created.
5. Final value looks like:
   `postgres://postgres.cdlefpvsvyepofsboepc:<PASSWORD>@aws-0-us-east-1.pooler.supabase.com:6543/postgres`

### How to set it

1. **GitHub repo → Settings → Environments → test → Environment secrets**.
2. **Add secret** `CI_SUPABASE_DB_URL`, paste the value, save.
3. Trigger the `CI` workflow via `workflow_dispatch` to verify the
   "Apply Supabase migrations to CI project" step succeeds.

### Rotation

If the DB password is rotated, update this secret to match. The CI job
fails closed with `::error::CI_SUPABASE_DB_URL secret is not set` if the
secret is missing.

## Failure modes

| Symptom in `Apply Supabase migrations to CI project` step | Likely cause |
|---|---|
| `password authentication failed for user "postgres..."` | DB password rotated; update the secret |
| `connection refused` / `timeout` | Project paused (free tier auto-pause); open the dashboard to wake it |
| `relation "supabase_migrations.schema_migrations" already exists` (warning, not error) | Normal — re-run is idempotent |
| `migration X.sql already applied` (info) | Normal |
| `must be owner of table` | Using anon/service-role JWT instead of postgres password — switch to the URI from Database settings, not the API tab |

## Why a brand-new project per environment

We have three Supabase projects: `dev`, `prod`, and this CI-only one. CI runs
write generated rows, schema_json, and build logs we don't want polluting
prod data and don't want hitting dev rate limits during nightly E2E.

## Related

- Issue #92 — original error context
- PR that wired this (TBD on merge) — `ci(supabase): apply migrations to CI project before E2E`
```

- [ ] **Step 3: Add the new secret to the test GitHub environment**

This is a manual one-time action (not committed code). Add a note to the runbook itself if the secret has not yet been added when this plan is executed:

```bash
# Verify the secret is set on the test environment:
gh api repos/stevenfackley/StackAlchemist/environments/test/secrets \
  --jq '.secrets[] | select(.name == "CI_SUPABASE_DB_URL") | .name'
```

If the output is empty, the user must add the secret via the Settings UI before the next CI run on `main`. Flag this in the PR description.

- [ ] **Step 4: Commit Phase C**

```bash
git add .github/workflows/ci.yml docs/runbooks/ci-supabase-migrations.md
git commit -m "$(cat <<'EOF'
ci(supabase): apply migrations to CI project before E2E

The CI-only Supabase project (ref cdlefpvsvyepofsboepc) was created without
schema, so the engine's PATCH /rest/v1/generations 400'd during nightly
simple-mode-flow E2E (issue #92, error #2).

- Add a "Apply Supabase migrations to CI project" step to e2e-integration
  job that runs `supabase db push --db-url $CI_SUPABASE_DB_URL --include-all`
  before backend services start. Idempotent (CLI tracks applied migrations).
- Fail closed if the secret is missing, with a pointer to the runbook.
- New runbook: docs/runbooks/ci-supabase-migrations.md covers how to obtain
  and rotate the secret, and common failure modes.

Requires the maintainer to add CI_SUPABASE_DB_URL to the test environment
secrets before the next push to main.

Refs #92.
EOF
)"
```

---

## Phase D — Verify and close

### Task D1: Push branch + open PR

- [ ] **Step 1: Push the branch and open a PR**

```bash
git push -u origin <branch-name>
gh pr create --title "fix: clear three engine errors blocking nightly simple-mode-flow E2E (#92)" \
  --body "$(cat <<'EOF'
## Summary
- Disable AWSSDK v4 checksum trailers on the R2 client (R2 returns 501) and add a typed bucket-existence probe.
- Bump Anthropic default to `claude-sonnet-4-6`; wire `ANTHROPIC_MODEL` end-to-end through `setup-env` + workflows + compose.
- Apply Supabase migrations to the CI project at the start of `e2e-integration` so the engine PATCH against `/rest/v1/generations` no longer 400s.

Closes #92.

## Test plan
- [ ] All engine unit tests pass locally (`dotnet test src/StackAlchemist.Engine.Tests/`)
- [ ] All web unit tests pass locally (`cd src/StackAlchemist.Web && npx vitest run`)
- [ ] Frontend lint + typecheck clean
- [ ] After merge, manually trigger `CI` workflow via `workflow_dispatch` and confirm `e2e-integration` runs the `Apply Supabase migrations` step successfully on the first run.
- [ ] Manually trigger nightly via `workflow_dispatch` (or wait for the 06:00 schedule) and confirm the dumped `sa-engine` logs show **no** Anthropic 404, **no** Supabase PATCH 400, **no** R2 PUT 501.

## Manual prerequisites (NOT in this PR)
- [ ] Add the `CI_SUPABASE_DB_URL` secret to the **test** GitHub environment. See `docs/runbooks/ci-supabase-migrations.md`.
- [ ] Optionally set the `ANTHROPIC_MODEL` repo variable to `claude-sonnet-4-6` (the workflow has it as a fallback so this is optional but explicit is better).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 2: Verify CI passes on the PR**

Watch the PR's checks. Expect `frontend`, `backend`, `docker`, `e2e-smoke` to all pass. `e2e-integration` does not run on PRs (it requires `workflow_dispatch` or `push` to `main`), so this is the boundary of what the PR can self-verify — note that fact in the PR comment.

If any check fails, debug from the run logs (`gh run view <id> --log-failed`) before merging.

---

### Task D2: After merge — manually run nightly and verify

- [ ] **Step 1: Set the CI secret if not already set**

Per the runbook (`docs/runbooks/ci-supabase-migrations.md`), confirm `CI_SUPABASE_DB_URL` is in the test environment. Run:

```bash
gh api repos/stevenfackley/StackAlchemist/environments/test/secrets --jq '.secrets[].name' | grep CI_SUPABASE_DB_URL
```

Expected: prints `CI_SUPABASE_DB_URL`. If not, set it in the GitHub UI before continuing.

- [ ] **Step 2: Manually dispatch CI on `main`**

```bash
gh workflow run CI --repo stevenfackley/StackAlchemist --ref main
sleep 5
gh run list --repo stevenfackley/StackAlchemist --workflow CI --limit 1
```

Get the run id, then watch:

```bash
gh run watch <RUN_ID> --repo stevenfackley/StackAlchemist
```

Expected: `e2e-integration` job runs (manual dispatch triggers it). Specifically watch:
- `Apply Supabase migrations to CI project` step — should print "Applying migration ..." for each of the 9 migrations on first run, then "no new migrations to apply" on subsequent runs.
- `Run Playwright Nightly Tests (real APIs, non-blocking)` step — runs but `continue-on-error: true`, so green even if flaky.
- `Dump backend container logs (always, on failure)` step — always runs; this is where the proof lives.

- [ ] **Step 3: Inspect engine logs for the three error signatures**

Download the `sa-engine` log block from the run. Look specifically for:

```bash
gh run view <RUN_ID> --repo stevenfackley/StackAlchemist --log \
  | grep -E "(api\.anthropic\.com|cdlefpvsvyepofsboepc.*PATCH|r2\.cloudflarestorage\.com.*PUT|404|400|501)" \
  | head -40
```

Expected:
- **No** `Anthropic API error 404` lines (LogApiError event id 802 with code 404).
- **No** `Supabase PATCH returned 400` lines (LogSupabasePatchNonOk event id 503 with code 400).
- **No** `501` from r2.cloudflarestorage.com.

If any are still present, capture the exact log line and reopen the relevant phase. Specifically:
- If 404 persists → the workflow var `ANTHROPIC_MODEL` is set to a stale value. Check `vars.ANTHROPIC_MODEL` and either unset it (so the `'claude-sonnet-4-6'` fallback applies) or update it.
- If 400 persists → check the `Apply Supabase migrations` step output for failures; ensure all 9 migrations applied. `psql $CI_SUPABASE_DB_URL -c "select * from supabase_migrations.schema_migrations"` (locally) confirms.
- If 501 persists → the checksum config did not take effect. Verify the deployed image is the new SHA, not a cached layer.

- [ ] **Step 4: Update issue #92 and close**

```bash
gh issue comment 92 --repo stevenfackley/StackAlchemist --body "$(cat <<'EOF'
Resolved by PR #<PR-NUM>. Verified in run <RUN-URL>:

- ✅ Anthropic 404 — engine default + ANTHROPIC_MODEL workflow var now both point at `claude-sonnet-4-6`.
- ✅ Supabase PATCH 400 — `Apply Supabase migrations to CI project` step lands all 9 migrations on the CI project before E2E starts.
- ✅ R2 PUT 501 — `RequestChecksumCalculation = WHEN_REQUIRED` reverts the AWSSDK v4 trailer-checksum behavior R2 rejects. New `R2BucketNotFoundException` / `R2BucketAccessDeniedException` make future bucket misconfig surface with a clear message.

Nightly suite is back to producing meaningful signal.
EOF
)"

gh issue close 92 --repo stevenfackley/StackAlchemist
```

- [ ] **Step 5: Update the Obsidian vault**

Per the workspace `CLAUDE.md`, log a short note in `claude-dev-projects/StackAlchemist/` capturing:
- Date (`2026-05-06`), PR #, commit SHAs.
- The reusable lesson: AWSSDK v4 + Cloudflare R2 → 501 unless `RequestChecksumCalculation = WHEN_REQUIRED`.
- Add a one-liner to `Resources/CI patterns & gotchas.md`: "AWSSDK.S3 v4 default checksum trailers break R2 — set RequestChecksumCalculation/ResponseChecksumValidation = WHEN_REQUIRED."

Use a single commit: `docs(obsidian): StackAlchemist — issue #92 R2 + Anthropic + Supabase fixes`.

---

## Self-review notes (writing-plans skill, applied at draft time)

- **Spec coverage:** All three issue errors mapped to phases (A, B, C). Verification is its own phase (D).
- **Placeholder scan:** No "TBD"/"implement later"/"add appropriate error handling". Every code step has actual code; every command has expected output.
- **Type consistency:** `R2BucketException` / `R2BucketNotFoundException` / `R2BucketAccessDeniedException` named the same way in tests, exception file, and translation method. `RequestChecksumCalculation` / `ResponseChecksumValidation` enum names match AWSSDK.S3 v4 (`Amazon.Runtime` namespace).
- **Open variables:** `<branch-name>` in D1 and `<RUN_ID>` / `<PR-NUM>` / `<RUN-URL>` in D2/D4 are runtime values — engineer fills in at execute time.
