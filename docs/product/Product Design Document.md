### Product Design Document (PDD): StackAlchemist

> Implementation status note (2026-04-07): Phases 1–5 are substantially complete. All features described in this document are implemented unless noted otherwise. The personalization wizard (Section 2, "Personalization Wizard UI") is live with all 4 steps. Multi-ecosystem platform selection (.NET/Next.js or FastAPI/React) is integrated into Advanced Mode Step 2. Remaining design-intent items: BYOK settings persistence UX (dashboard card is placeholder), and the "V1.5" Dapper ↔ EF Core toggle.

**1. System Overview**
StackAlchemist orchestrates code generation by connecting a Next.js frontend with a Supabase backend. It uses a dual mode intake flow and a hybrid template generation engine to compile custom business logic into a highly optimized .NET/Next.js repository, guaranteeing successful compilation before user delivery.

**2. UI/UX Design Specifications**
* **Theme:** Dark mode default. The implemented UI uses an elevated-slate palette: `#0F172A` and `#1E293B` as the deepest layers, `#334155` / `#475569` for cards and support surfaces, and `#4DA6FF` as the primary accent for active states, glows, and emphasis.
* **Component Library:** `shadcn/ui` styled with Tailwind CSS.
* **Landing Page:** The current landing page opens with a full-height hero focused on positioning copy and stack confidence, followed by a dedicated "Launch Console" section that contains mode switching, the prompt builder, and handoff guidance.
* **Simple Mode UI:** The home page uses a terminal-style prompt control with a prompt-builder layer that appends common architecture requirements. Upon submission, it transitions to a visual entity mapping canvas.
* **Advanced Mode UI:** A 4-step horizontal stepper wizard. Users define entities, choose the target platform (.NET/Next.js or FastAPI/React), configure API endpoints, and then select a tier, with real time relationship mapping visualization throughout.
* **Live Build Console:** During generation handoff and compile validation, the UI surfaces a terminal-style build log console fed by Supabase Realtime updates from the generation record.
* **Pricing UI:** The pricing page now uses the shared logo treatment and includes an explicit header path back to the home page.
* **Generation State UI:** A real time progress indicator utilizing Supabase WebSockets. It displays detailed logs including "Injecting Handlebars Templates," "Generating C# Controllers," and "Running Compile Guarantee CI Check."
* **Personalization Wizard UI:** A multi-step guided flow inserted between schema confirmation and generation trigger. Uses the existing horizontal stepper pattern with 4 steps:
  * Step 1 (Business Identity): Text inputs for business description, project name, and tagline. Clean card layout with helper copy explaining how the info will be used.
  * Step 2 (Color Scheme): A grid of 5-6 curated palette preset cards, each showing a swatch strip preview of primary/secondary/accent/background colors. Selecting a card highlights it; a "Custom" card opens a set of color pickers. A live mini-preview shows how the chosen palette renders on a sample UI card.
  * Step 3 (Domain Context): Dynamically rendered question cards — one per schema entity — with text inputs and example placeholder text. Questions adapt based on entity names from the confirmed schema.
  * Step 4 (Feature Toggles): A compact toggle/switch grid for optional features (auth method selector, soft-delete, audit timestamps, Swagger, Docker Compose). Each toggle has a one-line description.
  * A "Skip Personalization" link is visible on every step for users who prefer defaults. Progress indicator shows current step.

**3. API and Orchestration Design**
Next.js Server Actions manage the core logic to keep secrets secure.

* **Server Action: `GenerateSchemaFromPrompt(promptText)`**
    * Calls the LLM to translate natural language into the strict JSON schema required by the UI canvas.
* **Server Action: `SynthesizeArchitecture(generationId)`**
    * Fetches `payload_json`.
    * Executes the Handlebars compilation for static files.
    * Calls the LLM for dynamic file generation.
    * Reconstructs the directory.
* **Service: Build Validation (`CompileService` + `IBuildStrategy`)**
    * Implemented. `CompileWorkerService` runs as an in-process `BackgroundService` in the Engine. Dispatches to `DotNetBuildStrategy` or `PythonReactBuildStrategy` based on `ProjectType`. Max 3 retries with LLM error repair.
* **Service: File System & Storage (`CloudflareR2UploadService`)**
    * Implemented. Zips validated output directory, uploads to Cloudflare R2 via AWSSDK.S3, returns presigned download URL (168h expiry in production).

**4. V1 to V2 Stack Matrix Management**
The UI includes a configuration panel before generation begins.
* **V1 Configuration:** Hardcoded to .NET (Backend), Dapper (ORM), Next.js (Frontend), PostgreSQL (Database).
* The Next.js backend uses this configuration state to route the generation request to the correct template library directory on the server, ensuring the Handlebars engine loads the correct base files.
