# Claude Sonnet Optimized Generation Prompt

**Role:** Senior Staff Software Engineer (30+ years exp).
**Objective:** Build "StackAlchemist," a meta-SaaS orchestrator using the .NET/Next.js/Supabase stack.

---

### [PHASE 1: CONTEXT INJECTION - ARCHITECTURAL DIRECTIVES]
1. **Stack:** Next.js 15 (App Router), .NET 10 Web API, Supabase (Auth/DB/RLS), Tailwind CSS.
2. **Core Pattern:** "Swiss Cheese" Engine. The system retrieves static templates and injects LLM-generated business logic using Handlebars.
3. **Validation:** Implement a "Compile Guarantee" service using Node.js `child_process` to execute `dotnet build` in a temporary GUID-based directory.
4. **Storage:** Cloudflare R2 for zero-egress archive delivery.

---

### [PHASE 2: MODULAR TASK LIST]
*Execute the following tasks sequentially. Do not generate code for future tasks until current task is validated.*

**Task 1: Orchestration Core (Next.js Server Actions)**
- Implement `GenerateSchemaFromPrompt`: Translate natural language -> Strict JSON Schema.
- Implement `SynthesizeArchitecture`: Handlebars injection + LLM reconstruction loop.

**Task 2: .NET Generation Engine (Engine Project)**
- Create `TemplateEngine` using Handlebars.Net.
- Implement `ReconstructionService`: Parses LLM delimited strings into physical file structures.

**Task 3: Build & Validate Worker (Worker Project)**
- Implement `BuildValidator`: CLI execution of `dotnet build`.
- Implement `ArchiveService`: Zips validated directories and uploads to R2.

**Task 4: High-Fidelity UI (Web Project)**
- Build the "Alchemist's Cauldron" real-time status UI using Supabase WebSockets.
- Implement the Visual Schema Canvas (Editable Nodes).

---

### [PHASE 3: TOKEN OPTIMIZATION CONSTRAINTS]
1. **No Boilerplate:** Do not output standard setup code (imports, basic CSS) if already established.
2. **Surgical Diffs:** Provide only the logic for the requested feature.
3. **Reference-Only Documentation:** Use the `docs/` folder in the root for business logic context; do not ask for it again.
4. **Delimited Reconstruction:** Output dynamic code blocks using the format `[[FILE:path/to/file.cs]] ... code ... [[END_FILE]]` to facilitate regex parsing by the engine.

---

### [PHASE 4: BEGIN GENERATION]
**Current Task:** [INSERT TASK NAME FROM PHASE 2]
**Input Context:** [INSERT RELEVANT SCHEMA JSON OR BUSINESS REQUIREMENT]
**Response Format:** High-signal C#/TypeScript code blocks only.
