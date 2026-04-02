### Data Flow Diagram (DFD) Document: StackAlchemist

**1. Document Overview**
This document outlines the data flows within the StackAlchemist platform, illustrating how information moves between external entities, internal processes, and data stores. It is broken down into a Level 0 Context Diagram, a Level 1 System Diagram, and a Level 2 Process Diagram focusing specifically on the generation engine.



**2. Level 0: Context Diagram**
The Context Diagram represents StackAlchemist as a single, high-level process interacting with external entities.

* **External Entities:**
    * **User:** Provides natural language prompts, manual schema configurations, and payment information. Receives visual schema UI, live compilation statuses, and the final `.zip` archive URL.
    * **Stripe (Payment Gateway):** Receives checkout session requests. Sends webhook payloads confirming payment status.
    * **LLM API (Claude 3.5 Sonnet / BYOK):** Receives system prompts, tokenized context, and user schemas. Returns structured JSON schemas or specific code blocks.
* **Primary Data Flows:**
    * `User` -> [Business Requirements / API Key] -> `StackAlchemist System`
    * `StackAlchemist System` -> [Presigned Download URL / Schema UI] -> `User`
    * `StackAlchemist System` -> [Payment Intent] -> `Stripe`
    * `Stripe` -> [Webhook Transaction Status] -> `StackAlchemist System`
    * `StackAlchemist System` -> [RAG Context + Schema JSON] -> `LLM API`
    * `LLM API` -> [Generated Code / Error Fixes] -> `StackAlchemist System`



**3. Level 1: System Diagram**
This level breaks down the primary system into its core operational processes and local data stores.

* **Data Stores:**
    * **D1:** `profiles` (Supabase: User settings, encrypted BYOK keys)
    * **D2:** `transactions` (Supabase: Payment logs, access tiers)
    * **D3:** `generations` (Supabase: JSON payloads, status, R2 keys)
    * **D4:** Master Template Library (Server File System: .NET/Next.js base repos)
    * **D5:** Cloudflare R2 (Object Storage: Final `.zip` archives)

* **Processes and Flows:**
    * **Process 1.0: Intake & Auth**
        * Reads/Writes to **D1** for session management.
        * *Flow:* User inputs Prompt/Schema -> Process 1.0 parses into standard JSON -> Sends to Process 2.0.
    * **Process 2.0: Checkout Orchestration**
        * *Flow:* Receives tier selection -> Calls Stripe API -> Listens for Webhook -> Writes to **D2**.
        * *Flow:* Upon success, triggers Process 3.0.
    * **Process 3.0: The Generation Engine**
        * Reads JSON from **D3** and templates from **D4**.
        * *Flow:* Sends prompts to LLM API -> Receives dynamic code blocks.
        * *Flow:* Merges static templates and dynamic LLM code -> Sends to Process 4.0.
    * **Process 4.0: Compile Guarantee Pipeline**
        * *Flow:* Executes local `dotnet build`. 
        * *Flow (Error):* If exit code 1 -> Extracts standard error -> Sends retry prompt back to Process 3.0 / LLM API.
        * *Flow (Success):* If exit code 0 -> Sends validated directory to Process 5.0.
    * **Process 5.0: Storage & Delivery**
        * *Flow:* Zips directory -> Uploads to **D5** -> Updates **D3** with Presigned URL -> Delivers URL to User via UI WebSocket.



**4. Level 2: The "Swiss Cheese" Generation & Compile Guarantee**
This diagram details the specific internal data loops of Processes 3.0 and 4.0.

* **Process 3.1: Template Hydration (Handlebars)**
    * *Input:* JSON Schema (from User) + Static Templates (from D4).
    * *Action:* Injects deterministic data (e.g., project name, DB connection strings, basic IaC scripts for Tier 3) into the boilerplate files.
    * *Output:* Hydrated base directory.
* **Process 3.2: Dynamic Context Compilation**
    * *Input:* JSON Schema.
    * *Action:* Formats the schema and system instructions into the prompt context window. Checks D1 for user's specific BYOK API key; defaults to platform Claude key if null.
* **Process 3.3: LLM Code Synthesis**
    * *Input:* Prompt Context.
    * *Action:* Sends request to LLM API. Receives delimited string containing custom Dapper queries, C# models, and Next.js pages.
* **Process 3.4: Reconstruction Parser**
    * *Input:* LLM delimited string.
    * *Action:* Parses string via regex. Injects specific code blocks into the target files within the Hydrated base directory (Process 3.1).
    * *Output:* Completed Source Code Directory.
* **Process 4.1: CLI Build Execution**
    * *Input:* Completed Source Code Directory.
    * *Action:* Spawns child process for `dotnet build`.
* **Process 4.2: Error Handling Loop**
    * *Input:* `stderr` output from Process 4.1.
    * *Action:* If build fails, formats `stderr` into a correction prompt. Routes data back to Process 3.3 (Maximum 3 iterations).
    * *Output:* Validated Source Code Directory (routed to packing and delivery).