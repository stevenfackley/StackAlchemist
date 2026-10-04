### Product Requirements Document (PRD): StackAlchemist

> Status note (2026-10-03): the platform stack changed after this document was first written. Sign-in is Qavren Auth (a Keycloak realm via Auth.js, email/password and Google), data lives in qavren-db (Postgres, owner-scoped queries in code, no row-level security), generation status is polled rather than pushed over WebSockets, the default model is Claude Sonnet 5.5, and there is no staging environment (every merge to `main` deploys production). Supabase is legacy and being removed. Sections below that still say Supabase describe the original design.

> Audit note (2026-04-04, updated 2026-07): this document captures both implemented and target requirements. The audited repository now includes substantial Phase 3–6 implementation (generation orchestrator, compile worker, Stripe webhook/session backend, Supabase migrations, auth pages, dashboard shell). The personalization wizard and BYOK are now live: BYOK keys are encrypted in the dashboard and decrypted per generation in the Engine, which routes to the user's chosen provider/model (Anthropic BYOK, OpenAI, OpenRouter).

**1. Executive Summary**
StackAlchemist converts user requirements into deployable software repositories. The platform bypasses the boilerplate setup phase by dynamically injecting AI generated business logic into a heavily optimized master template, delivering a custom PostgreSQL database schema, backend, and Next.js frontend.

**2. Core Features and Functional Requirements**

**User Intake and Authentication**
* Secure login and session management via Qavren Auth (Keycloak realm, Auth.js session).
* **Dual Mode Intake UX:**
     * **Simple Mode:** The current app presents a terminal-style prompt control on the landing page with prompt-builder presets that help visitors compose a usable brief quickly. The target flow remains: call the LLM to generate a JSON schema, then render it visually for review and editing.
     * **Advanced Mode:** A dynamic UI wizard to manually define entity models, relationships, and API endpoints.
* **Multi-Step Personalization Wizard:**
     * After schema confirmation (in either mode), users walk through a guided personalization flow that customizes the generated output so no two projects look or feel the same.
     * **Business Identity:** Users describe their business (industry, audience, value prop), provide a project/company name and optional tagline. This context is injected into generated READMEs, code comments, environment configs, and seed data.
     * **Color Scheme & Branding:** Users choose from curated color palette presets (e.g., "Corporate Blue," "Warm Startup," "Dark Hacker," "Earthy Minimal," "Bold SaaS") or define a fully custom palette. The selection maps to Tailwind CSS color tokens injected into the generated frontend theme.
     * **Domain Vocabulary & Specifics:** Adaptive contextual questions based on the user's schema entities (e.g., "What does an 'Order' represent in your business?"). Responses enrich the LLM prompt so generated controllers, validation rules, seed data, and UI copy use realistic domain language.
     * **Feature Preferences (Optional):** Toggles for cross-cutting concerns — authentication method, soft-delete, audit timestamps, Swagger/OpenAPI docs, Docker Compose inclusion. These map to Handlebars feature flags in the templates.
     * The wizard is skippable (sensible defaults applied) but is the default recommended path.
* **Marketing Conversion UX:**
    * The home screen must clearly communicate the three delivery depths: architecture artifacts, generated codebase, and infrastructure handoff.
    * The pricing page must provide a clear path back to the home page via the top logo/header navigation.

**Billing and Tier Access**
* Stripe integration for payment processing.
* **Tier 1:** Unlocks schema and API documentation downloads.
* **Tier 2:** Unlocks the full downloadable zip archive of the codebase.
* **Tier 3:** Unlocks the codebase plus customized AWS CDK/Terraform scripts, Helm Charts for Kubernetes, Docker Compose files, and step by step deployment runbooks.

**Generation Engine & Phased Rollout**
* **V1 Stack:** The latest version of .NET Web API, Dapper micro ORM, Next.js frontend, and PostgreSQL.
* **Python stack (shipped):** FastAPI + React as an alternative to .NET + Next.js, selected in Advanced Mode.
* **V2 templates (shipped, Development only):** `V2-DotNet-NextJs` and `V2-Python-React` fill each injection zone with its own parallel LLM call. Production still runs the V1 one-shot path (`Generation:UseSwissCheese` is off outside Development).
* **Not built:** the Dapper/Entity Framework Core toggle (formerly V1.5) and the Node.js/Prisma and Nuxt.js alternatives (formerly V2).
* The system defaults to Claude Sonnet 5.5 for production generation with Bring Your Own Key (BYOK) support for Anthropic (Sonnet 5.5, Opus 5.5, Haiku 4.5), OpenAI, or OpenRouter.
* The generation utilizes a "Swiss Cheese" method: core plumbing is handled via static files and Handlebars templates, while dynamic logic is generated by the LLM.

**Compile Guarantee Pipeline**
* The backend must reconstruct the physical directory structure locally.
* The system must execute a CLI build command (e.g., `dotnet build`) on the generated directory.
* If the build fails, the system parses the error output and sends an automated retry prompt to the LLM for correction.
* The application only zips the directory and generates the Cloudflare R2 presigned download URL upon a successful build.

**3. Non Functional Requirements**
* **Performance:** The UI must show generation progress as it happens. Implemented by polling the owner-scoped `getGeneration` server action every 3 seconds while the tab is visible.
* **Security:** All user prompts must be sanitized. BYOK API keys must be encrypted at rest (AES-256-GCM) in the platform database. Every query is scoped to the signed-in user in code.
* **Environments:** There is a single production environment: one AWS EC2 host running Docker Compose behind a Cloudflare Tunnel. There is no staging or test environment; CI (including real-build template gates and Playwright runs) is the gate before merge.
* **Design Consistency:** Marketing pages must share the elevated-slate dark palette and electric-blue accent system defined in `docs/branding/Branding Guidelines.md`.
