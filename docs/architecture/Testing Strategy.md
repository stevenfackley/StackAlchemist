# StackAlchemist: Comprehensive Testing Strategy

This document defines the failsafe testing strategy for the StackAlchemist platform. It covers unit, integration, end-to-end, contract, chaos, and visual regression testing across all three codebases (Web, Engine, Worker) and their external integrations.

---

## 1. Testing Philosophy

StackAlchemist's core value proposition — **compile-guaranteed code generation** — demands an exceptionally robust test suite. An untested generation pipeline is a broken product. Every layer of the system must be independently verifiable and collectively validated.

**Principles:**
- **Test the contract, not the implementation.** Mock at service boundaries, not inside services.
- **Golden files over mocks for LLM output.** Real (canned) LLM responses catch real parsing bugs.
- **Compile verification is a first-class test.** If the generated code doesn't build, the product is broken.
- **No flaky tests in CI.** Any test that fails intermittently is deleted or fixed immediately.
- **Fail loudly.** Silent failures (swallowed exceptions, missing files, empty outputs) must be caught by assertions.

---

## 2. Test Pyramid Overview

```
         ╱ ╲
        ╱ E2E ╲          Playwright browser tests (few, slow, high confidence)
       ╱───────╲
      ╱ Integr. ╲        Docker Compose cross-service tests (moderate)
     ╱───────────╲
    ╱   Contract   ╲     LLM output parsing, API response shapes (moderate)
   ╱─────────────────╲
  ╱     Unit Tests     ╲  Vitest + xUnit (many, fast, isolated)
 ╱─────────────────────────╲
```

---

## 3. Frontend Testing (Next.js 15 / TypeScript)

### 3.1 Frameworks & Tools

| Tool | Purpose |
|------|---------|
| **Vitest** | Unit + integration test runner (ESM-native, fast) |
| **React Testing Library (RTL)** | Component rendering and interaction |
| **@testing-library/user-event** | Realistic user interaction simulation |
| **MSW (Mock Service Worker)** | API mocking at the network level |
| **Playwright** | E2E browser testing + visual regression |

### 3.2 Test File Structure

```
src/StackAlchemist.Web/
├── __tests__/
│   ├── components/
│   │   ├── SimpleMode.test.tsx        # Terminal textarea + submit flow
│   │   ├── AdvancedWizard.test.tsx     # Stepper wizard navigation + validation
│   │   ├── EntityCanvas.test.tsx       # React Flow canvas CRUD operations
│   │   ├── TierPricing.test.tsx        # Pricing card rendering + Stripe redirect
│   │   ├── Dashboard.test.tsx          # Generation history + re-download
│   │   ├── BYOKSettings.test.tsx       # Key input, validation, save
│   │   └── ErrorBoundary.test.tsx      # Fallback UI rendering on error
│   ├── lib/
│   │   ├── schema-parser.test.ts       # JSON schema validation + transformation
│   │   ├── auth.test.ts                # Supabase auth helpers
│   │   └── api-client.test.ts          # API call utilities
│   └── mocks/
│       ├── handlers.ts                 # MSW request handlers
│       ├── server.ts                   # MSW server setup
│       └── fixtures/
│           ├── schema-extraction-response.json
│           └── generation-status-updates.json
├── e2e/
│   ├── simple-mode-flow.spec.ts        # Full Simple Mode journey
│   ├── advanced-mode-flow.spec.ts      # Full Advanced Mode journey
│   ├── checkout-flow.spec.ts           # Stripe checkout redirect
│   ├── dashboard.spec.ts               # Authenticated dashboard
│   └── visual/
│       ├── landing-page.spec.ts        # Visual regression: landing page
│       └── entity-canvas.spec.ts       # Visual regression: React Flow canvas
├── vitest.config.ts
├── vitest.setup.ts
└── playwright.config.ts
```

### 3.3 Unit Test Targets

| Component / Module | Key Assertions |
|--------------------|----------------|
| **SimpleMode textarea** | Renders terminal prompt; submits on Enter/button; shows loading state; transitions to canvas on response |
| **AdvancedWizard** | Step 1→2→3 navigation; back button works; form validation blocks forward; final payload matches schema |
| **EntityCanvas (React Flow)** | Add/edit/delete entity nodes; drag to create relationships; JSON output matches visual state |
| **TierPricing** | Correct prices render; Stripe checkout session created with correct tier + amount |
| **Dashboard** | Shows generation history; status badges match state; download links are clickable |
| **BYOK Settings** | Key input masked; save triggers API call; invalid key shows error |
| **Schema Parser** | Valid JSON passes; missing required fields rejected; relationship references validated |
| **Error Boundaries** | Child error → fallback UI rendered; error details logged |

### 3.4 E2E Test Targets (Playwright)

| Flow | Steps |
|------|-------|
| **Simple Mode** | Load landing → click Simple Mode → type prompt → submit → wait for canvas → verify entities rendered → edit entity → confirm schema → assert payload sent to backend |
| **Advanced Mode** | Load landing → click Advanced → Step 1: add entities → Step 2: choose platform → Step 3: configure endpoints → Step 4: select tier → verify schema JSON |
| **Checkout** | Select tier → Stripe Checkout redirect → verify session params |
| **Dashboard** | Login → view generation history → click download → verify link resolves |

### 3.5 Visual Regression

Playwright screenshots capture baseline images of key UI states. CI compares against baselines and fails on unexpected pixel differences. Targets:
- Landing page (hero, pricing, footer)
- Entity canvas with 3+ nodes and relationships
- Dashboard with mixed generation statuses
- Simple Mode terminal with typing animation

---

## 4. Backend Testing (.NET 10 Engine + Worker)

### 4.1 Frameworks & Tools

| Tool | Purpose |
|------|---------|
| **xUnit** | Test runner (industry standard for .NET) |
| **NSubstitute** | Mocking framework (clean API, no Castle.Core issues) |
| **FluentAssertions** | Readable assertion syntax |
| **Testcontainers.PostgreSql** | Real PostgreSQL in Docker for integration tests |
| **Microsoft.AspNetCore.Mvc.Testing** | `WebApplicationFactory` for API integration tests |
| **Verify** | Snapshot testing for complex objects (optional) |

### 4.2 Test Project Structure

```
src/
├── StackAlchemist.Engine.Tests/
│   ├── StackAlchemist.Engine.Tests.csproj
│   ├── Services/
│   │   ├── ReconstructionServiceTests.cs
│   │   ├── TemplateProviderTests.cs
│   │   ├── PromptBuilderTests.cs
│   │   ├── TierGatingServiceTests.cs
│   │   └── SchemaExtractionServiceTests.cs
│   ├── Controllers/
│   │   └── GenerationControllerTests.cs
│   ├── Webhooks/
│   │   └── StripeWebhookTests.cs
│   ├── Integration/
│   │   ├── GenerationPipelineTests.cs
│   │   ├── DatabaseIntegrationTests.cs
│   │   └── R2StorageIntegrationTests.cs
│   └── Fixtures/
│       └── LlmResponses/
│           ├── single-entity-valid.txt
│           ├── multi-entity-valid.txt
│           ├── entity-with-relationships.txt
│           ├── malformed-delimiters.txt
│           ├── truncated-response.txt
│           ├── extra-markdown-wrapping.txt
│           ├── duplicate-file-blocks.txt
│           └── empty-file-block.txt
│
├── StackAlchemist.Worker.Tests/
│   ├── StackAlchemist.Worker.Tests.csproj
│   ├── CompileWorkerTests.cs
│   ├── StateMachineTests.cs
│   ├── RetryLogicTests.cs
│   └── Integration/
│       └── CompileGuaranteeIntegrationTests.cs
```

### 4.3 Unit Test Targets — ReconstructionService (CRITICAL)

The ReconstructionService is the most critical component in the system. It parses raw LLM text output into discrete files. Every edge case must be covered:

| Test Case | Input | Expected Behavior |
|-----------|-------|-------------------|
| **Happy path** | Well-formed `[[FILE:path]]...[[END_FILE]]` blocks | Returns dictionary of path → content |
| **Multiple files** | 5+ file blocks | All files extracted correctly |
| **Missing END_FILE** | Block without closing delimiter | Throws `MalformedLlmOutputException` with context |
| **Missing FILE header** | Content before any `[[FILE:]]` | Ignores preamble content |
| **Empty file block** | `[[FILE:path]][[END_FILE]]` | Returns empty string for that path (valid) |
| **Duplicate paths** | Two blocks with same path | Last one wins (logged as warning) |
| **Unexpected paths** | Path outside expected template structure | Logged as warning, still included |
| **Truncated response** | Response ends mid-block (token limit) | Throws `TruncatedLlmResponseException` |
| **Markdown wrapping** | ` ```csharp ` around content | Strips markdown fences before extraction |
| **BOM characters** | UTF-8 BOM at start of content | Strips BOM, content is clean |
| **Mixed line endings** | `\r\n` and `\n` mixed | Normalizes to `\n` |
| **Whitespace in path** | `[[FILE: src/Controller.cs ]]` | Trims whitespace from path |
| **Nested delimiters** | `[[FILE:]]` appearing in code comments | Only matches at line start |

### 4.4 Unit Test Targets — TemplateProvider

| Test Case | Expected Behavior |
|-----------|-------------------|
| **Render all variables** | `{{ProjectName}}`, `{{DbConnectionString}}`, etc. replaced correctly |
| **Missing required variable** | Throws `MissingTemplateVariableException` (never silently outputs `{{var}}`) |
| **LLM injection zones identified** | All `{{!-- LLM_INJECTION_START/END --}}` zones found and listed |
| **Injection into zones** | LLM content placed between START/END markers |
| **Template not found** | Throws `TemplateNotFoundException` with available templates listed |
| **Nested partials** | Handlebars partials resolve correctly |

### 4.5 Unit Test Targets — Compile Guarantee Worker (State Machine)

| Test Case | Expected Behavior |
|-----------|-------------------|
| **Happy path** | `pending → generating → building → packing → uploading → success` |
| **Build failure → retry** | `building → generating` (retry) with build error appended to prompt |
| **Retry includes error context** | Retry prompt contains the specific `dotnet build` error output |
| **Retry #2 includes all errors** | Retry prompt contains errors from attempt 1 AND attempt 2 |
| **Max retries exceeded** | After 3 failures → status = `failed`, `retry_count` = 3 |
| **Concurrent generations** | Two simultaneous jobs don't interfere (isolated temp directories) |
| **Retry prompt under token limit** | Accumulated errors don't push prompt past `max_tokens` — oldest errors trimmed |
| **npm build also validated** | After `dotnet build` succeeds, `npm build` runs for Next.js portion |
| **Partial build success** | .NET builds but npm fails → retries with npm error context |

### 4.6 Unit Test Targets — Tier Gating

| Test Case | Expected Behavior |
|-----------|-------------------|
| **Tier 1 (Blueprint)** | Returns schema + API docs; no code generation triggered |
| **Tier 2 (Boilerplate)** | Full code generation pipeline runs |
| **Tier 3 (Infrastructure)** | Code generation + IaC templates + Helm charts + deployment runbook |
| **Invalid tier** | Throws `InvalidTierException` |
| **Tier mismatch** | Transaction tier doesn't match generation request → rejected |

### 4.7 Unit Test Targets — Stripe Webhook

| Test Case | Expected Behavior |
|-----------|-------------------|
| **Valid signature** | Event processed, transaction created, generation triggered |
| **Invalid signature** | Returns 401 Unauthorized |
| **Replay attack (old timestamp)** | Returns 401 Unauthorized |
| **Duplicate event ID** | Returns 200 OK (idempotent) but no duplicate generation |
| **Unexpected event type** | Returns 200 OK, no action taken, logged |
| **Missing required fields** | Returns 400 Bad Request with details |

### 4.8 Unit Test Targets — BYOK Encryption

| Test Case | Expected Behavior |
|-----------|-------------------|
| **Encrypt/decrypt round-trip** | Original key recovered after encrypt → store → decrypt |
| **Different keys don't collide** | Two users' encrypted keys are different ciphertexts |
| **Empty key** | Treated as "no BYOK" — platform key used |
| **Malformed key** | Validation catches invalid API key format before encryption |

---

## 5. Integration Testing

### 5.1 Docker Compose Test Environment

The `docker/docker-compose.test.yml` provides the cross-service integration test environment:

```
┌──────────────────────────────────────────────────────┐
│  docker-compose.test.yml                             │
│  ┌──────────┐  ┌───────────┐  ┌──────────────┐      │
│  │ sa-web   │  │ sa-engine  │  │  sa-worker   │      │
│  └──────────┘  └───────────┘  └──────────────┘      │
│  ┌──────────────────┐  ┌────────────────────┐        │
│  │ Supabase (local) │  │ Mock LLM Server    │        │
│  └──────────────────┘  └────────────────────┘        │
│  ┌──────────────────┐  ┌────────────────────┐        │
│  │ MinIO (R2 mock)  │  │ Stripe CLI (test)  │        │
│  └──────────────────┘  └────────────────────┘        │
└──────────────────────────────────────────────────────┘
         ↑ Playwright + xUnit integration tests run against this
```

### 5.2 Integration Test Targets

| Test | Services Involved | What It Validates |
|------|-------------------|-------------------|
| **Full generation pipeline** | Engine + Worker + Mock LLM + MinIO | Submit schema → prompt built → mock LLM responds → files reconstructed → compiled → zipped → uploaded → download URL works |
| **Auth + RLS** | Web + Supabase | User A cannot see User B's generations |
| **Payment → Generation** | Web + Engine + Stripe CLI | Stripe webhook → transaction created → generation triggered with correct tier |
| **WebSocket streaming** | Web + Engine + Supabase Realtime | Generation status updates stream to frontend in real-time |
| **BYOK routing** | Engine + Mock LLM | Custom API key used when set; platform key used when not |
| **Rate limiting** | Web + Engine | Excessive requests return 429 before reaching generation logic |
| **Schema extraction** | Web + Engine + Mock LLM | Natural language prompt → extracted JSON schema → valid React Flow data |

### 5.3 Mock LLM Server

For integration tests, a lightweight HTTP server returns canned LLM responses based on the prompt content. This avoids API costs and rate limits while testing the full pipeline.

**Implementation:** A simple Express.js or .NET minimal API that:
1. Receives the same request shape as the Anthropic API
2. Pattern-matches on prompt keywords (e.g., "Product entity" → returns the product golden file)
3. Returns properly formatted `[[FILE:path]]...[[END_FILE]]` response
4. Can be configured to return malformed responses for chaos testing

---

## 6. LLM-Specific Testing

### 6.1 Golden File Tests (Snapshot Testing for LLM Output)

Maintain a library of **real** (or realistic) LLM responses saved as text fixtures:

```
src/StackAlchemist.Engine.Tests/Fixtures/LlmResponses/
├── single-entity-valid.txt          # 1 entity (Product), clean output
├── multi-entity-valid.txt           # 5 entities, no relationships
├── entity-with-relationships.txt    # 3 entities with FK relationships
├── complex-schema.txt               # 10+ entities, many-to-many
├── malformed-delimiters.txt         # Missing [[END_FILE]] tags
├── truncated-response.txt           # Cut off mid-file (simulates token limit)
├── extra-markdown-wrapping.txt      # ```csharp fences around code
├── duplicate-file-blocks.txt        # Same file path appears twice
└── empty-file-block.txt             # [[FILE:path]][[END_FILE]] with no content
```

**Usage:** Unit tests parse each golden file through the ReconstructionService and assert expected behavior. When prompts are updated, the golden file suite is re-run to detect regressions.

### 6.2 Prompt Regression Testing

- All prompt templates are version-controlled in `src/StackAlchemist.Engine/Prompts/`
- CI tracks token count per prompt version (logged in test output)
- A prompt change triggers mandatory re-run of the golden file suite
- Prompt token count exceeding threshold triggers a CI warning

### 6.3 Chaos Testing for LLM Output

Intentionally malformed inputs test system resilience:

| Chaos Scenario | Expected System Behavior |
|----------------|--------------------------|
| Truncated at 50% of expected output | `TruncatedLlmResponseException` → retry with "please complete all files" appended |
| Wrong delimiter style (`---FILE:path---`) | `MalformedLlmOutputException` → retry with delimiter format reminder |
| Extra conversational text mixed in | Preamble/postscript ignored; only delimited blocks extracted |
| Valid C# but wrong namespace/class name | `dotnet build` catches it → retry with build error |
| HTML/XSS in generated code comments | Sanitization strips dangerous content before packaging |

---

## 7. Database Testing

### 7.1 Migration Testing

Using `supabase db test` (pgTAP under the hood):
- Verify all tables exist with correct columns and types
- Verify RLS policies enforce row isolation
- Verify foreign key constraints work correctly
- Verify indexes exist on frequently queried columns

### 7.2 RLS Policy Tests

| Test | Assertion |
|------|-----------|
| User A queries `generations` | Only sees their own rows |
| User A queries `transactions` | Only sees their own rows |
| Unauthenticated query | Returns zero rows |
| Service role query | Sees all rows (for admin/worker) |

### 7.3 Integration with Testcontainers

For .NET integration tests that need database access without Supabase:
- Spin up PostgreSQL via Testcontainers
- Apply migrations
- Run tests against real SQL
- Container destroyed after test run

---

## 8. CI/CD Pipeline with Test Gates

### 8.1 Pull Request Pipeline (`.github/workflows/ci.yml`)

```yaml
# Triggered on every PR to main
steps:
  # Frontend gates
  - npm run lint                    # ESLint zero errors
  - npm run type-check              # TypeScript strict zero errors  
  - npx vitest run                  # Unit + integration tests
  
  # Backend gates
  - dotnet build                    # Engine + Worker compile
  - dotnet test                     # xUnit unit + integration tests
  
  # Docker gates
  - docker build --target web .     # Web image builds
  - docker build --target engine .  # Engine image builds
  - docker build --target worker .  # Worker image builds
  
  # Database gates
  - supabase db test                # Migration + RLS tests
  
  # E2E gates (on merge queue or nightly)
  - docker compose -f docker/docker-compose.test.yml up -d
  - npx playwright test             # Full E2E suite
```

### 8.2 Gate Requirements

| Gate | Blocking? | Rationale |
|------|-----------|-----------|
| Lint | ✅ Yes | Code quality baseline |
| Type check | ✅ Yes | Catches type errors at compile time |
| Unit tests | ✅ Yes | Core logic correctness |
| dotnet build | ✅ Yes | Backend must compile |
| dotnet test | ✅ Yes | Backend logic correctness |
| Docker builds | ✅ Yes | Deployability guarantee |
| DB migration tests | ✅ Yes | Schema integrity |
| E2E (Playwright) | ⚠️ Merge queue | Slower, runs before merge |
| Visual regression | ⚠️ Manual review | Screenshot diffs require human approval |

---

## 9. Test Data & Fixture Strategy

| Data Type | Local Dev | CI | Staging |
|-----------|-----------|-----|---------|
| **Database** | Supabase CLI (`supabase start`) | Testcontainers (PostgreSQL) | Supabase `develop` branch |
| **LLM responses** | Golden file fixtures in repo | Same fixtures | Mock LLM server (Docker) |
| **Stripe** | Stripe test mode + `stripe listen` | Stripe test mode | Stripe test mode |
| **R2/S3 storage** | MinIO in Docker | MinIO in Docker | Cloudflare R2 (test bucket) |
| **Auth tokens** | Supabase local auth | Mocked JWT tokens | Supabase `develop` branch |
| **Email (Resend)** | Resend test mode (sink) | Mocked | Resend test mode |

---

## 10. Test Naming Conventions

### Frontend (Vitest)
```typescript
describe('SimpleMode', () => {
  it('should render the terminal textarea with placeholder text', () => {});
  it('should show loading spinner on prompt submission', () => {});
  it('should transition to entity canvas when schema is received', () => {});
  it('should display error toast when API call fails', () => {});
});
```

### Backend (xUnit)
```csharp
public class ReconstructionServiceTests
{
    [Fact]
    public void Parse_WithValidDelimitedBlocks_ReturnsAllFiles() { }
    
    [Fact]
    public void Parse_WithMissingEndDelimiter_ThrowsMalformedOutputException() { }
    
    [Theory]
    [MemberData(nameof(GoldenFileTestCases))]
    public void Parse_GoldenFile_ProducesExpectedOutput(string fixtureName, int expectedFileCount) { }
}
```

---

## 11. Coverage Targets

| Codebase | Line Coverage Target | Branch Coverage Target |
|----------|---------------------|----------------------|
| **ReconstructionService** | 95%+ | 90%+ |
| **TemplateProvider** | 90%+ | 85%+ |
| **State Machine (Worker)** | 95%+ | 95%+ |
| **Stripe Webhook** | 90%+ | 90%+ |
| **Tier Gating** | 100% | 100% |
| **Frontend components** | 80%+ | 70%+ |
| **Frontend utilities** | 90%+ | 85%+ |
| **Overall** | 80%+ | 75%+ |

**Note:** Coverage targets are guidelines, not hard CI gates. A 79% coverage with excellent tests is better than 95% coverage with trivial assertions.

---

## 12. When to Write Tests (Development Workflow)

1. **Before writing a feature** — Write the test for the expected behavior first (TDD for critical paths like ReconstructionService, state machine, tier gating).
2. **During feature development** — Write tests alongside code for non-critical paths.
3. **After a bug is found** — Write a regression test that reproduces the bug before fixing it.
4. **Before merging** — All CI gates must pass. No exceptions.

---

## 13. Package Versions (Pinned for Stability)

### Frontend
| Package | Version |
|---------|---------|
| vitest | ^3.x |
| @testing-library/react | ^16.x |
| @testing-library/user-event | ^14.x |
| @testing-library/jest-dom | ^6.x |
| msw | ^2.x |
| @playwright/test | ^1.50.x |

### Backend (.NET)
| Package | Version |
|---------|---------|
| xunit | 2.9.x |
| xunit.runner.visualstudio | 3.x |
| NSubstitute | 5.x |
| FluentAssertions | 8.x |
| Testcontainers.PostgreSql | 4.x |
| Microsoft.AspNetCore.Mvc.Testing | 10.x |
| coverlet.collector | 6.x |
