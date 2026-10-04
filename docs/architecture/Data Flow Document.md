### Data Flow Diagram (DFD) Document: StackAlchemist

> Updated 2026-10-03: data stores are qavren-db tables, the LLM is Claude Sonnet 5.5, and status reaches the user by polling.

**1. Document Overview**
This document outlines the data flows within the StackAlchemist platform, illustrating how information moves between external entities, internal processes, and data stores. It is broken down into a Level 0 Context Diagram, a Level 1 System Diagram, and a Level 2 Process Diagram focusing specifically on the generation engine.

**2. Level 0: Context Diagram**
The Context Diagram represents StackAlchemist as a single, high-level process interacting with external entities.

* **External Entities:**
    * **User:** Provides natural language prompts, manual schema configurations, and payment information. Receives the visual schema UI, build status, and the final `.zip` download URL.
    * **Keycloak (Qavren Auth):** Hosts sign-in, registration, email verification and password reset. Issues the identity the web app turns into a JWT session.
    * **Stripe (Payment Gateway):** Receives checkout session requests. Sends webhook payloads confirming payment status.
    * **LLM API (Claude Sonnet 5.5 or a BYOK provider):** Receives system prompts, context and user schemas. Returns structured JSON schemas or code blocks.
* **Primary Data Flows:**
    * `User` -> [Business Requirements / API Key] -> `StackAlchemist System`
    * `StackAlchemist System` -> [Presigned Download URL / Schema UI] -> `User`
    * `StackAlchemist System` -> [Checkout Session Request] -> `Stripe`
    * `Stripe` -> [Webhook Transaction Status] -> `StackAlchemist System`
    * `StackAlchemist System` -> [Schema JSON + Context] -> `LLM API`
    * `LLM API` -> [Generated Code / Error Fixes] -> `StackAlchemist System`

**3. Level 1: System Diagram**
This level breaks down the primary system into its core operational processes and local data stores.

* **Data Stores** (all in qavren-db, schema `stackalchemist`, except D4 and D5):
    * **D1:** `profiles` (user settings, encrypted BYOK key, preferred model)
    * **D2:** `transactions` and `stripe_events` (payment records, tiers, webhook idempotency)
    * **D3:** `generations` (JSON payloads, status, build log, download URL, Spark preview files)
    * **D4:** Master Template Library (template sets shipped with the Engine: `V0-Spark-NextJs`, `V1-*`, `V2-*`, `Tier3-Infrastructure`)
    * **D5:** Cloudflare R2 (object storage: final `.zip` archives)

* **Processes and Flows:**
    * **Process 1.0: Intake & Auth**
        * Authenticates through Keycloak; reads/writes **D1** for settings.
        * *Flow:* User inputs Prompt/Schema -> Process 1.0 parses into standard JSON -> inserts a `pending` row into **D3** owned by the session user -> Process 2.0 (paid tiers) or Process 3.0 (Spark).
    * **Process 2.0: Checkout Orchestration**
        * *Flow:* Receives tier selection -> Engine calls Stripe API -> Stripe webhook arrives at the Engine -> writes **D2** atomically (`process_checkout_completed`).
        * *Flow:* Upon success, enqueues Process 3.0.
    * **Process 3.0: The Generation Engine**
        * Reads JSON from **D3** and templates from **D4**.
        * *Flow:* Sends prompts to LLM API -> receives code blocks.
        * *Flow:* Merges the template and the generated code -> sends to Process 4.0.
    * **Process 4.0: Compile Guarantee Pipeline**
        * *Flow:* Runs the real build for the project type.
        * *Flow (Error):* On failure, extracts the error output and sends a repair prompt to the LLM (max 3 repairs). On final failure of a paid tier, refunds via Stripe.
        * *Flow (Success):* Sends the validated directory to Process 5.0.
    * **Process 5.0: Storage & Delivery**
        * *Flow:* Zips the directory -> uploads to **D5** -> updates **D3** with the presigned URL. The user's browser reads **D3** through the owner-scoped `getGeneration` server action, polled every 3 s while the tab is visible. Spark builds write `preview_files_json` to **D3** instead of a ZIP.

**4. Level 2: The Generation & Compile Guarantee**
This level details the internal data loops of Processes 3.0 and 4.0. It shows the V1 one-shot path that prod runs; the V2 "Swiss Cheese" path (Development only) replaces 3.2 to 3.4 with per-zone parallel injection.

* **Process 3.1: Template Hydration (Handlebars)**
    * *Input:* JSON Schema (from User) + Static Templates (from D4).
    * *Action:* Injects deterministic data (project name, connection strings, personalization, basic IaC for Tier 3) into the boilerplate files.
    * *Output:* Hydrated base directory.
* **Process 3.2: Dynamic Context Compilation**
    * *Input:* JSON Schema, personalization.
    * *Action:* Formats the schema and system instructions into the prompt (sanitized). Reads the user's BYOK key from D1 (decrypted only in the Engine); defaults to the platform key if none.
* **Process 3.3: LLM Code Synthesis**
    * *Input:* Prompt context.
    * *Action:* Sends the request to the LLM API. Receives a string of `[[FILE:path]]` blocks.
* **Process 3.4: Reconstruction Parser**
    * *Input:* LLM delimited string.
    * *Action:* Parses the blocks and writes them into the hydrated base directory.
    * *Output:* Completed source code directory.
* **Process 4.1: CLI Build Execution**
    * *Input:* Completed source code directory.
    * *Action:* Spawns the build via the project type's strategy: `dotnet build` plus `npm ci`, typecheck and `npm run build`; or, for Python + React, pip install in a per-build venv, lint, pytest, then the npm half.
* **Process 4.2: Error Handling Loop**
    * *Input:* Error output from Process 4.1.
    * *Action:* If the build fails, formats the errors into a repair prompt and routes back to Process 3.3 (maximum 3 repairs).
    * *Output:* Validated source code directory, routed to packing and delivery (with `build-report.json` in the archive).
