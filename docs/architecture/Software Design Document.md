### Software Design Document (SDD): StackAlchemist

**1. System Architecture**
* **Frontend and API Gateway:** Next.js application (App Router) handling user intake and checkout.
* **State and Identity:** Supabase PostgreSQL for relational data and Auth. Row Level Security restricts generation history access.
* **Generation Engine:** Claude 3.5 Sonnet API. Local testing utilizes a Proxmox environment running Ollama.
* **Storage Layer:** Cloudflare R2 provides zero egress temporary storage for compiled archives.

**2. DevOps & Environment Strategy**
The platform utilizes a strict 3-tier environment structure. Secrets are managed via dedicated `.env` files which are never committed to version control.

* **Development (`.env.development`):** Runs locally on Windows/Linux hybrid machines. Uses local Supabase instances, mocked Stripe endpoints, and a dev-specific Cloudflare R2 bucket.
* **Test / Staging (`.env.test`):** Runs in a Proxmox LXC container routed via Cloudflare Tunnels (`test.stackalchemist.app`). Secrets are injected dynamically via GitHub Actions or Docker Compose.
* **Production (`.env.production`):** Hosted on AWS. Secrets are injected at build time via GitHub Actions Secrets. Connects to the live Stripe integration and production Supabase project.

**3. Core Workflows and Technical Pipelines**

**A. Dual Mode Intake Pipeline**
* **Simple Mode:** User submits text prompt. Next.js server action calls LLM to generate a structured JSON schema. The frontend renders this JSON into an editable node based UI.
* **Advanced Mode:** User interacts directly with the node based UI to define entities. The final validated schema is serialized into a JSON payload.

**B. The "Swiss Cheese" Generation Method**
The system maintains a library of master templates (starting with the V1 .NET/Next.js stack).
1.  **Template Retrieval:** Node.js backend retrieves the static boilerplate and Handlebars templates.
2.  **Deterministic Injection:** Handlebars injects project names and standard configurations.
3.  **LLM Generation:** The LLM receives the database schema and generates only the dynamic files (Dapper queries, C# controllers, Next.js pages).
4.  **Reconstruction:** The LLM generated files are injected into the specific placeholders within the master template directory.

**C. The Compile Guarantee CI/CD**
1.  The Node.js backend spawns a child process to run `dotnet build` in the temporary reconstruction directory.
2.  If exit code `0` is returned, the process proceeds to compression.
3.  If exit code `1` is returned, the standard error output is captured and sent back to the LLM with the context of the broken file for an automated fix. Maximum retry limit is set to 3.

**D. Tier 3 IaC Export Pipeline**
For Tier 3 transactions, the system utilizes Handlebars to inject the user's specific environment variables and project naming conventions into pre written AWS CDK scripts and Docker Compose files. A markdown runbook is generated and included in the final root directory of the zip archive.

**3. Supabase Data Schema**
* `profiles`: `id` (UUID, PK), `api_key_override` (Encrypted string), `preferred_model` (String), `created_at` (Timestamp).
* `transactions`: `id` (UUID, PK), `user_id` (UUID, FK), `stripe_session_id` (String), `tier` (Integer), `status` (String).
* `generations`: `id` (UUID, PK), `user_id` (UUID, FK), `transaction_id` (UUID, FK), `payload_json` (JSONB), `r2_object_key` (String), `status` (String).