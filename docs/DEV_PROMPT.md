# StackAlchemist: Master Development Prompt

**Instructions for the User:** Copy and paste everything below the line into your Claude Code (4.6 Sonnet) CLI to initiate the project build.

---

**Role:** You are Claude 4.6 Sonnet, acting as a Senior Staff Software Engineer. Your objective is to build the "StackAlchemist" platform — a micro-SaaS that converts natural language or manual schema definitions into fully scaffolded, compile-verified, downloadable code repositories.

**Critical Distinction:** You (the developer) are 4.6 Sonnet. The application you are building (StackAlchemist) will make API calls to **Claude 3.5 Sonnet** to generate code for end-users. Never configure the StackAlchemist product to call the 4.6 API.

### [PROJECT STRUCTURE]
```
src/
  StackAlchemist.Web/            # Next.js 15 App Router (frontend + server actions)
  StackAlchemist.Engine/         # .NET 10 Web API (generation orchestrator)
  StackAlchemist.Worker/         # Background compile-guarantee worker
  StackAlchemist.Templates/      # Handlebars master templates
    V1-DotNet-NextJs/            #   V1 stack: .NET + Dapper + Next.js + PostgreSQL
docs/
  product/                       # PRD, BRD, Use Cases, Wireframes, Design Doc
  architecture/                  # SDD, ERD, Sequence Diagrams, State Machine, Data Flow
  branding/                      # Color tokens, typography, component styling rules
```

### [DESIGN SYSTEM MANDATE]
All UI must follow the design language in `docs/branding/Branding Guidelines.md`. Key constraints:
- **Dark mode only.** Base layers use `#0F172A` and `#1E293B`, supported by `#334155` / `#475569` slate surfaces. The primary accent is `#4DA6FF`, used for active states, glows, and feature emphasis rather than broad fills.
- **Typography:** Inter for all UI text. JetBrains Mono for code blocks, terminal prompt, and generation logs.
- **Borders:** Thin 1px borders in electric blue or slate gray. The app now uses a mix of 4px, 12px, 16px, and larger 24px to 28px hero panel radii on marketing surfaces; do not force every surface back to sharp corners.
- **Glassmorphism:** Subtle `backdrop-blur-md` on modals and floating toolbars only.
- **Aesthetic:** "Technical alchemy" — precise, dark, electric, but not sterile. The current home flow uses a full-screen narrative hero first, then a separate launch console section with prompt-builder actions and handoff summaries. Preserve that direction instead of collapsing everything into one crowded viewport.

---

### [PHASE 1: PROJECT SCAFFOLDING & UI FOUNDATIONS] (Zero External Keys Needed)

**Objective:** Standing projects that compile, with the two core intake UIs rendering.

1. Read the `claude.md` file in the root directory to understand your operational constraints.
2. Read all files in `docs/product/`, `docs/architecture/`, and `docs/branding/` to ingest full project context.
3. Initialize the Next.js 15 App Router in `src/StackAlchemist.Web` (Tailwind, shadcn/ui). Apply the design system tokens from the branding guidelines.
4. Initialize the .NET 10 Web API in `src/StackAlchemist.Engine`.
5. Build the landing page UI per `docs/product/wireframes.md`.
6. **Simple Mode:** Build the CLI-style terminal textarea. On submit, display a mock visual entity-relationship canvas (use React Flow) showing hardcoded entities — this previews the flow where Claude 3.5 will later extract a JSON schema from natural language. The user should be able to edit entities/relationships on this canvas.
7. **Advanced Mode:** Build the horizontal stepper wizard (Step 1: Define Entities → Step 2: Configure API Endpoints → Step 3: Select Tier & Pay). Use structured inputs (dropdowns, text fields) and render a real-time relationship visualization using React Flow.

**Definition of Done:**
- `npm run dev` serves the landing page at `localhost:3000` without errors.
- Simple Mode textarea renders and transitions to a mock entity canvas on submit.
- Advanced Mode wizard renders all 3 steps with navigation.
- `dotnet build` succeeds on the Engine project.
- `npm run lint` passes with zero errors.

*Compile, ensure UI renders, and ask for approval to proceed.*

---

### [PHASE 2: MASTER TEMPLATE CONSTRUCTION] (Zero External Keys Needed)

**Objective:** Complete "Swiss Cheese" template library — static scaffolding with Handlebars variables and delimited LLM injection zones.

1. Create `src/StackAlchemist.Templates/V1-DotNet-NextJs/`.
2. Write the base structural files for V1 generation:
   - .NET: `Program.cs`, `appsettings.json`, `.csproj`, base middleware, Dapper connection setup.
   - Next.js: `layout.tsx`, `page.tsx`, `tailwind.config.ts`, `package.json`.
   - Infra: `Dockerfile`, `docker-compose.yml`, `.env.example`.
3. Insert Handlebars variables (`{{ProjectName}}`, `{{DbConnectionString}}`, `{{Entities}}`, etc.) into these templates for deterministic config injection.
4. Insert clearly delimited placeholder zones where the LLM will inject dynamic business logic:
   ```
   {{!-- LLM_INJECTION_START: Controllers --}}
   {{!-- LLM_INJECTION_END: Controllers --}}
   ```
   These zones must exist for: Controllers, Dapper repository classes, Next.js pages, API route handlers.

**Definition of Done:**
- Template directory contains all base files with valid Handlebars syntax.
- A manual Handlebars render with mock data produces syntactically valid project files.

*Ask for approval to proceed.*

---

### [PHASE 3: THE GENERATION ENGINE & COMPILE WORKER] (Zero External Keys Needed)

**Objective:** Working generation pipeline — template loading, LLM output parsing, file reconstruction, and compile-guarantee retry loop — all tested with mocked LLM output.

1. **TemplateProvider:** In the .NET project, implement the service that loads Phase 2 templates and renders Handlebars variables.
2. **ReconstructionService:** Build the parser that processes delimited LLM output blocks:
   ```
   [[FILE:src/Controllers/ProductsController.cs]]
   // generated code here
   [[END_FILE]]
   ```
   It must extract each file block and inject them into the correct template placeholder zones.
3. **LLM Prompt Template:** Create a version-controlled prompt file at `src/StackAlchemist.Engine/Prompts/V1-generation.md`. This is the system prompt sent to Claude 3.5 Sonnet at runtime. It must:
   - Instruct 3.5 to output ONLY `[[FILE:path]]...[[END_FILE]]` delimited blocks.
   - Specify the exact file paths expected for the V1 stack.
   - Include few-shot examples of correct output format.
   - Set `temperature: 0.3` and appropriate `max_tokens` in the calling code.
4. **Compile Guarantee Worker:** Build the background worker (`src/StackAlchemist.Worker/`) that:
   - Runs `dotnet build` on the reconstructed project directory.
   - **On failure:** Extracts the build error output, appends it as correction context to the original LLM prompt, re-calls the LLM (mocked for now), reconstructs, and retries.
   - **Max retries: 3.** Track `retry_count`. After 3 failures, mark generation as `failed`.
   - Implement the state machine from `docs/architecture/Generation State Machine.md`: `pending → generating → building → success | failed`.
5. **Local E2E Test:** Create a hardcoded mock string representing a valid Claude 3.5 response. Run the full pipeline: load templates → inject mock LLM output → reconstruct files → compile. Verify the generated project builds successfully.
6. **Unit Tests:** Write tests for:
   - `ReconstructionService` — correct parsing of delimited blocks, handling of malformed output.
   - `TemplateProvider` — Handlebars rendering with various input schemas.
   - Compile worker — retry logic and state transitions.

**Definition of Done:**
- `dotnet test` passes all unit tests.
- The local E2E test produces a directory that passes `dotnet build`.
- The state machine transitions are logged and verifiable.

*Compile, run the local end-to-end test, and ask for approval.*

---

### [PHASE 4: EXTERNAL INTEGRATIONS] (USER INTERVENTION REQUIRED)

**Objective:** Replace all mocks with live services. Full pipeline: user submits prompt → schema generated → code generated → compiled → zipped → uploaded → download link returned.

*At this phase, instruct the user to provide keys in `.env` for: Supabase (URL, anon key, service role key), Anthropic API key, Cloudflare R2 (account ID, access key, secret key, bucket name).*

1. **Database & Auth:** 
   - Set up Supabase Auth (email/password + OAuth providers).
   - Implement the database schema **exactly as documented in `docs/architecture/Database ERD.md`**:
     - `profiles` table (id, email, api_key_override encrypted, preferred_model, created_at) with RLS.
     - `transactions` table (id, user_id, stripe_session_id, tier, amount, status, created_at) with RLS.
     - `generations` table (id, user_id, transaction_id, payload_json, status, retry_count, r2_object_key, created_at, completed_at) with RLS.
   - Wire the UI to save/load schemas to the `generations.payload_json` column.
2. **Schema Extraction (Simple Mode):** Implement the core Simple Mode flow:
   - User submits natural language prompt → Next.js server action calls Claude 3.5 Sonnet with a schema-extraction system prompt → receives structured JSON schema → renders it on the React Flow entity canvas for user editing.
   - This is a separate LLM call from the code generation call. Create its own prompt template at `src/StackAlchemist.Engine/Prompts/V1-schema-extraction.md`.
3. **Code Generation (LLM Integration):** Replace the Phase 3 mock with the actual Anthropic SDK call to Claude 3.5 Sonnet using the `V1-generation.md` prompt.
4. **Storage:** Implement the AWS SDK (S3-compatible) to zip the validated output and upload to Cloudflare R2, returning a presigned download URL.
5. **WebSockets:** Wire Supabase Realtime to stream generation status updates and build logs to the frontend UI. Implement the "Alchemist's Cauldron" progress UI: three-phase indicator (Analyzing → Transmuting → Validating) with a timestamped log stream.
6. **Structured Logging:** Implement structured JSON logging from the .NET Engine and Worker. Log: what prompt was sent, token count, what came back, build output, retry attempts. This is the generation audit trail.

**Definition of Done:**
- A user can sign up, submit a Simple Mode prompt, see the extracted schema on the canvas, confirm, and receive a generated + compiled zip file via presigned R2 URL.
- Advanced Mode wizard also produces a generation through the same pipeline.
- Build logs stream to the UI in real-time.
- All database tables exist with RLS enforced.

*Test the full pipeline with live APIs and ask for approval.*

---

### [PHASE 5: MONETIZATION & DASHBOARD] (USER INTERVENTION REQUIRED)

*Instruct the user to provide Stripe keys (publishable + secret + webhook signing secret) in `.env`.*

1. **Stripe Integration:** Implement Stripe Checkout Sessions for the 3 pricing tiers:
   - Tier 1 — Blueprint ($299): Schema + API docs download only.
   - Tier 2 — Boilerplate ($599): Full codebase zip download.
   - Tier 3 — Infrastructure ($999): Codebase + IaC templates + deployment runbook.
2. **Webhook Orchestration:** Build a secure webhook endpoint (`/api/webhooks/stripe`) to handle `checkout.session.completed` events. Verify signatures. Insert into `transactions` table and trigger the generation pipeline.
3. **Tier Gating:** Update the Engine to respect the purchased tier — Tier 1 skips code generation (schema + docs only), Tier 2 generates code, Tier 3 generates code + IaC.
4. **User Dashboard:** Build the authenticated dashboard with:
   - Generation history with status indicators and re-download links (valid R2 presigned URLs).
   - **BYOK (Bring Your Own Key):** A settings panel where users can input their own API keys (Anthropic, OpenAI, OpenRouter). Store encrypted in `profiles.api_key_override`. When set, the Engine uses their key instead of the platform key.
   - Model preference selector (stored in `profiles.preferred_model`).
5. **Integration Tests:** Write tests for:
   - Stripe webhook signature verification.
   - Tier gating logic (correct outputs per tier).
   - BYOK key encryption/decryption round-trip.

**Definition of Done:**
- Stripe Checkout redirects correctly for all 3 tiers.
- Webhook processes test events and creates transaction + triggers generation.
- Dashboard shows generation history with working re-download links.
- BYOK input saves encrypted and is used for subsequent generations.
- `stripe listen --forward-to localhost` test passes end-to-end.

*Test Stripe webhooks locally and ask for approval.*

---

### [PHASE 6: TIER 3 IaC & PLATFORM CI/CD]

**Objective:** Tier 3 deliverables for end-users + production infrastructure for the platform itself.

1. **Tier 3 Templates:** Create Handlebars templates for end-user infrastructure:
   - AWS CDK stack (VPC, ECS/Fargate, RDS PostgreSQL, ALB).
   - Docker Compose (local development orchestration).
   - **Helm Charts** (Kubernetes deployment, service, ingress, configmap, secrets).
   - Terraform alternative for multi-cloud users.
2. **Deployment Runbook:** Dynamically generate a `DEPLOYMENT.md` step-by-step runbook tailored to the user's project, including:
   - Helm install/upgrade instructions with value overrides.
   - AWS CDK bootstrap and deploy commands.
   - Environment variable reference.
   - DNS and SSL setup guidance.
3. **Platform Dockerization:** Implement the root `Dockerfile` using a **multi-stage, multi-target strategy**:
   - Target `sa-web`: Next.js standalone build.
   - Target `sa-engine`: .NET 10 publish.
   - Target `sa-worker`: .NET 10 worker publish.
   - Shared base layers where possible to minimize image size.
4. **CI/CD Pipeline:** Implement GitHub Actions with **Environments (Test and Prod)**:
   - Secrets scoped to environments without prefixes (e.g., `SUPABASE_ANON_KEY` not `TEST_SUPABASE_ANON_KEY`).
   - **Test pipeline:** On push to `main` → build all 3 targets → push to GHCR → deploy to Proxmox staging via SSH.
   - **Prod pipeline:** On release tag (`v*`) → build → push to GHCR → deploy to AWS (ECS or similar).
   - Include: linting, unit tests, Docker build validation as CI gates.

**Definition of Done:**
- `docker build --target sa-web .` / `sa-engine` / `sa-worker` all succeed.
- Tier 3 generation produces valid Helm charts, CDK stack, and runbook.
- GitHub Actions YAML passes `actionlint` validation.
- CI pipeline runs successfully in a dry-run mode.

*Verify Docker builds and CI/CD YAML syntax, then ask for approval.*

---

### [PHASE 7: PRODUCTION HARDENING & LAUNCH]

**Objective:** Security, legal, email, observability — everything needed for a production launch.

1. **Legal Pages:** Stub out Terms of Service and Privacy Policy pages with placeholder legal text. Wire into the footer and signup flow.
2. **Transactional Emails:** Integrate Resend. Implement:
   - "Your Stack is Ready" email with the R2 download link on generation success.
   - "Generation Failed" email with support contact on final failure.
   - Welcome email on signup.
3. **Security Hardening:**
   - Strict rate limiting on all API endpoints (especially generation and LLM-calling routes) to prevent abuse and DDoS.
   - Input sanitization on the prompt textarea to mitigate prompt-injection attacks against the Claude 3.5 calls.
   - CORS policy locked to production domain.
   - CSP headers on the Next.js frontend.
4. **Error Boundaries:** Add React error boundaries on all major UI sections (canvas, dashboard, generation status) with user-friendly fallback UIs.
5. **Observability:**
   - Health check endpoints on Engine and Worker.
   - Structured error logging with correlation IDs across the generation pipeline.
   - Generation metrics: success rate, average compile retries, average generation time.

**Definition of Done:**
- Rate limiting returns 429 on excessive requests.
- Emails send successfully via Resend in test mode.
- Error boundaries render fallback UI when triggered.
- Health check endpoints return 200.
- All `npm run lint`, `dotnet build`, and `dotnet test` pass.

*Final review before Go-Live.*

---

### [EXECUTION DIRECTIVE]

**CRITICAL WORKFLOW:**
1. **Before beginning ANY phase:** You MUST read the entire `docs/product/`, `docs/architecture/`, and `docs/branding/` directories to refresh your context.
2. **Execute the phase.**
3. **After completing ANY phase:** You MUST:
   - Review and update every file in `docs/product/` and `docs/architecture/` to reflect the actual architectural decisions and codebase state you just implemented.
   - Append a summary of decisions made to `docs/DECISIONS.md` (create it if it doesn't exist). This is your persistent memory across phases — reference it instead of re-reading source files when possible.
   - Create a git commit following Conventional Commits (e.g., `feat: complete phase 1 scaffolding`).
   - Create a git tag: `phase-N-complete` (e.g., `phase-1-complete`) so we can roll back cleanly if needed.

**CONTEXT MANAGEMENT:**
- You have a 1M token context window. Ingest the docs once per phase, then work from memory.
- Prefer importing/referencing existing code over re-reading entire files.
- Use surgical edits (`replace` / precise diffs) to save tokens. Do not rewrite entire files for small changes.
- Use your CLI access to validate your own work (`dotnet build`, `npm run lint`, `dotnet test`, `npm run dev`).

**TESTING MANDATE:**
- Every phase that produces business logic must include unit tests.
- The generation pipeline (Phase 3+) must have integration tests that verify the full mock-to-compiled-output flow.
- Run `dotnet test` and confirm all tests pass before declaring a phase complete.

Begin from the highest incomplete phase shown in the Progress Tracker below. Do not rewrite already completed phase work. First read the current docs and decisions log, then continue from the existing scaffold and implemented services in the repository.

---

### [PROGRESS TRACKER]

**Last Updated:** 2026-04-03

#### PHASE 1: PROJECT SCAFFOLDING & UI FOUNDATIONS — COMPLETE

**Implemented and verified in repo:**
- Next.js 15.5 App Router frontend in `src/StackAlchemist.Web/` (Tailwind 3, Inter + JetBrains Mono, dark-only styling)
- .NET 10 Web API host in `src/StackAlchemist.Engine/`
- Landing page with full-height hero + separate Launch Console section and prompt-builder UX
- Simple Mode page and Advanced Mode 3-step wizard with React Flow entity tooling
- Additional marketing/product pages now present: `/about`, `/story`, `/docs`, `/pricing`
- Build/lint hardening from prior audit remains in place

#### PHASE 2: MASTER TEMPLATE CONSTRUCTION — COMPLETE

**Implemented and verified in repo:**
- `src/StackAlchemist.Templates/V1-DotNet-NextJs/` template set with `dotnet/`, `nextjs/`, `infra/`
- Handlebars variables and LLM injection zones across backend/frontend/infra templates
- Template validation utility (`validate.mjs`) for deterministic render checks

#### PHASE 3: GENERATION ENGINE & COMPILE WORKER — COMPLETE

**Implemented and verified in repo:**
- `TemplateProvider` implemented (`LoadTemplate`, `Render`, injection zone helpers)
- `ReconstructionService` implemented (`[[FILE:path]]...[[END_FILE]]` parsing + template zone reconstruction)
- `GenerationOrchestrator` implemented: render templates → call LLM client → parse/reconstruct → write temp output → enqueue compile job
- Compile state machine actively used in orchestration + worker transitions
- `CompileWorkerService` implemented with retry loop (`MaxRetries = 3`), build error extraction, LLM repair pass, and state updates
- Engine tests cover prompt building, schema extraction validation, tier gating, and integration-adjacent services

#### PHASE 4: EXTERNAL INTEGRATIONS — IN PROGRESS (MAJOR PARTS IMPLEMENTED)

**Implemented now:**
- Live/Mock LLM client switching in Engine based on config (`AnthropicLlmClient` vs `MockLlmClient`)
- Cloudflare R2 zip upload service (`CloudflareR2UploadService`) with presigned URL return
- Supabase delivery sync service (`SupabaseDeliveryService`) for generation status + download URL updates
- In-process queue architecture (Engine + Worker combined process via Channel + hosted service)
- Stripe webhook endpoint scaffolded at `/api/webhooks/stripe` with signature validation and enqueue behavior
- Web frontend generation status route (`/generate/[id]`) with realtime subscription + status-step UI
- Spark/free tier UX implemented in web app:
  - `tier: 0` type support
  - free preview path with `preview_files_json`
  - embedded micro-IDE experience + paid upgrade CTAs
- Pricing now explicitly documents Spark (free) + paid tiers in both cards and comparison matrix

**Still not fully complete for Phase 4 Definition of Done:**
- End-to-end authenticated Supabase schema/RLS rollout for `profiles`/`transactions`/`generations` still documented as planned architecture
- Fully wired schema-extraction LLM path for Simple Mode is only partially represented (service exists; complete integrated runtime flow still pending)
- Confirmed production-grade WebSocket/build-log streaming semantics and full live API proof are not yet declared complete in docs

#### PHASE 5: MONETIZATION & DASHBOARD — PARTIALLY STARTED

**Implemented now:**
- Pricing architecture and tier semantics are live in UI, including Spark free preview path
- Stripe webhook receiver exists in Engine and can enqueue generation jobs

**Pending:**
- Full checkout/session lifecycle wiring from frontend
- Authenticated dashboard with generation history/BYOK/model preference
- Complete transaction persistence model and strict tier gating end-to-end

#### PHASE 6: TIER 3 IaC & PLATFORM CI/CD — NOT STARTED (BEYOND TEMPLATE STUBS)
#### PHASE 7: PRODUCTION HARDENING & LAUNCH — NOT STARTED

#### RECOMMENDED CURRENT STARTING POINT

1. Close Phase 4 gaps first (true live schema extraction flow, RLS-backed production schema, and verified end-to-end generation pipeline).
2. Then complete Phase 5 (full Stripe checkout orchestration + authenticated dashboard + BYOK).
3. Keep `docs/architecture/*` and `docs/product/*` synced as each integration milestone is finished.
