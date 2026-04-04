### Software Design Document (SDD): StackAlchemist

> Implementation status note (2026-04-04): this repository is now beyond Phase 1 scaffold status. The audited codebase includes a live .NET generation orchestrator, compile worker retry loop, Stripe webhook + checkout-session endpoint, Cloudflare R2 upload service, Supabase delivery/status sync, Supabase migrations for `profiles`/`transactions`/`generations`, and authenticated dashboard/login flows. Remaining gaps are primarily end-to-end checkout wiring polish, BYOK settings persistence UI, and the personalization wizard.

**1. System Architecture**
* **Frontend and API Gateway:** Next.js application (App Router) handling user intake and checkout.
* **State and Identity:** Supabase PostgreSQL for relational data and Auth. Row Level Security restricts generation history access.
* **Generation Engine:** Claude 3.5 Sonnet API. Local testing utilizes a Proxmox environment running Ollama.
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
    * **Trigger:** GitHub Release/Tag.
    * **Actions:** 
        * **git-cliff:** Automatically generate `CHANGELOG.md` and create a GitHub Release.
        * **AWS Deploy:** Merge **Supabase `develop` -> `main`** and deploy verified images to AWS.

**5. Core Workflows and Technical Pipelines**

**A. Dual Mode Intake Pipeline**
* **Simple Mode:** User submits text prompt. Next.js server action calls LLM to generate a structured JSON schema. The frontend renders this JSON into an editable node based UI.
* **Advanced Mode:** User interacts directly with the node based UI to define entities. The final validated schema is serialized into a JSON payload.

**B. The "Swiss Cheese" Generation Method**
The system maintains a library of master templates (starting with the V1 .NET/Next.js stack).
1.  **Template Retrieval:** The planned generation engine retrieves the static boilerplate and Handlebars templates.
2.  **Deterministic Injection:** Handlebars injects project names and standard configurations.
3.  **LLM Generation:** The LLM receives the database schema and generates only the dynamic files (Dapper queries, C# controllers, Next.js pages).
4.  **Reconstruction:** The LLM generated files are injected into the specific placeholders within the master template directory.

**C. The Compile Guarantee CI/CD**
1.  The future worker process spawns a child process to run `dotnet build` in the temporary reconstruction directory.
2.  If exit code `0` is returned, the process proceeds to compression.
3.  If exit code `1` is returned, the standard error output is captured and sent back to the LLM with the context of the broken file for an automated fix. Maximum retry limit is set to 3.

**D. Tier 3 IaC Export Pipeline**
For Tier 3 transactions, the system utilizes Handlebars to inject the user's specific environment variables and project naming conventions into pre written AWS CDK scripts, Terraform providers, and **Helm Charts**. A markdown runbook is generated and included in the final root directory of the zip archive.

**6. Supabase Data Schema**
* Planned Phase 4 schema: `profiles`, `transactions`, and `generations` as documented in the ERD.
* Current Phase 1 frontend code references a temporary `generations` shape with `mode`, `tier`, `prompt`, `schema_json`, `download_url`, `error_message`, and `attempt_count`.
* No checked-in production migration or RLS policy currently exists for the live application database; the only SQL artifact in the repo is the template placeholder used for generated projects.
