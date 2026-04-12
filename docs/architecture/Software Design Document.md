### Software Design Document (SDD): StackAlchemist

> Implementation status note (2026-04-12): The current V1 stack is live and production-facing. The codebase includes in-process generation orchestration (template render → LLM → reconstruction → compile retry), multi-ecosystem support (DotNet-NextJs + Python-React via `IBuildStrategy`), personalization, Stripe Checkout + webhook payment gate, Supabase SSR auth with user-linked generations, Cloudflare R2 upload, real-time build log streaming, and security hardening (rate limiting, CORS, service key auth, prompt sanitization, token accounting, schema size limits, mobile navigation, metadata, sitemap, robots, and route error boundaries). Tier 3 IaC templates remain pending.

**1. System Architecture**
* **Frontend and API Gateway:** Next.js application (App Router) handling user intake and checkout.
* **State and Identity:** Supabase PostgreSQL for relational data and Auth. Row Level Security restricts generation history access.
* **Generation Engine:** Anthropic Claude 3.5 Sonnet API in production, with a mock fallback when no Anthropic key is configured.
* **Storage Layer:** Cloudflare R2 provides zero egress temporary storage for compiled archives.

**2. DevOps & Environment Strategy**
The platform utilizes a **Single Project + Branching** model in Supabase, synchronized with GitHub Actions **Environments** (Test and Prod).

* **Development (`.env.development`):** Local development using the Supabase CLI (`supabase start`).
* **Test (`.env.test`):** Scoped to the "Test" environment in GitHub. Connects to the **Supabase `develop` branch**.
* **Prod (`.env.production`):** Scoped to the "Prod" environment in GitHub. Connects to the **Supabase `main` project**.

**3. Containerization Strategy**
StackAlchemist uses a multi-stage, multi-target Docker architecture. This allows a single `Dockerfile` to serve all environments by targeting specific build stages (`web`, `engine`, `worker`).

**4. Optimal CI/CD Flow**
* **Stage 1: Continuous Integration (CI)**
    * **Trigger:** Pull Requests.
    * **Actions:** Run tests and verify Supabase migrations using `supabase db test`.
* **Stage 2: Staging Deployment (Test)**
    * **Trigger:** Push to `develop` branch.
    * **Actions:** Build and replace the `sa-web` container on the test host for the public test site.
* **Stage 3: Production Deployment (Prod)**
    * **Trigger:** Merge to `main` branch.
    * **Automation:**
        * **git-cliff:** Automatically update `CHANGELOG.md` and create a GitHub Release for every push to `main`.
        * **AWS Deploy:** Deploy verified images to the production environment (EC2) automatically.
    * **Health Checks:** Components utilize `wget` for health probes (e.g., `sa-web` and `sa-engine` final stages include `wget` to support Docker Compose health checks).

**5. Core Workflows and Technical Pipelines**

**A. Dual Mode Intake Pipeline**
* **Simple Mode:** User submits text prompt. Next.js server action calls LLM to generate a structured JSON schema. The frontend renders this JSON into an editable node based UI.
* **Advanced Mode:** User interacts directly with the node based UI to define entities, choose a target platform, and configure endpoints. The final validated schema plus `project_type` selection are serialized into the generation payload.
* **Compile Orchestration:** `CompileService` now dispatches to per-platform build strategies so `.NET/Next.js` and `FastAPI/React` projects can share the same retry/state machine pipeline while keeping ecosystem-specific validation commands and error parsing.

**B. The "Swiss Cheese" Generation Method**
The system maintains a library of master templates (starting with the V1 .NET/Next.js stack).
1.  **Template Retrieval:** The planned generation engine retrieves the static boilerplate and Handlebars templates.
2.  **Deterministic Injection:** Handlebars injects project names and standard configurations.
3.  **LLM Generation:** The LLM receives the database schema and generates only the dynamic files (Dapper queries, C# controllers, Next.js pages).
4.  **Reconstruction:** The LLM generated files are injected into the specific placeholders within the master template directory.

**C. The Compile Guarantee CI/CD**
1.  The in-process compile worker runs `dotnet build` in the temporary reconstruction directory.
2.  If exit code `0` is returned, the process proceeds to compression.
3.  If exit code `1` is returned, the standard error output is captured and sent back to the LLM with the context of the broken file for an automated fix. Maximum retry limit is set to 3.

**D. Tier 3 IaC Export Pipeline**
For Tier 3 transactions, the system utilizes Handlebars to inject the user's specific environment variables and project naming conventions into pre written AWS CDK scripts, Terraform providers, and **Helm Charts**. A markdown runbook is generated and included in the final root directory of the zip archive.

**6. Supabase Data Schema**
* Three tables implemented with checked-in Supabase migrations: `profiles`, `transactions`, `generations` — matching the ERD.
* Six migrations in `supabase/migrations/`: create_profiles, create_transactions, create_generations, add_project_type, add_personalization, add_token_usage_and_atomic_build_log.
* RLS enforced: users read/update own profiles; read own transactions; anyone inserts generations (free tier); service role manages updates. Realtime publication enabled on `generations`.
* `generations` table includes: `schema_json`, `personalization_json`, `build_log`, `preview_files_json`, `project_type`, `download_url`, `status`, `retry_count`, `input_tokens`, `output_tokens`, `model_used`, `user_id`.
