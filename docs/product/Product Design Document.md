### Product Design Document (PDD): StackAlchemist

> Implementation status note (2026-04-02): the current codebase implements the landing page, Simple Mode mock flow, and Advanced Mode wizard from Phase 1. The server actions, real-time generation pipeline, checkout, and file delivery behaviors below remain design intent and are not fully wired end to end yet.

**1. System Overview**
StackAlchemist orchestrates code generation by connecting a Next.js frontend with a Supabase backend. It uses a dual mode intake flow and a hybrid template generation engine to compile custom business logic into a highly optimized .NET/Next.js repository, guaranteeing successful compilation before user delivery.

**2. UI/UX Design Specifications**
* **Theme:** Dark mode default. The implemented UI uses an elevated-slate palette: `#0F172A` and `#1E293B` as the deepest layers, `#334155` / `#475569` for cards and support surfaces, and `#4DA6FF` as the primary accent for active states, glows, and emphasis.
* **Component Library:** `shadcn/ui` styled with Tailwind CSS.
* **Landing Page:** The current landing page opens with a full-height hero focused on positioning copy and stack confidence, followed by a dedicated "Launch Console" section that contains mode switching, the prompt builder, and handoff guidance.
* **Simple Mode UI:** The home page uses a terminal-style prompt control with a prompt-builder layer that appends common architecture requirements. Upon submission, it transitions to a visual entity mapping canvas.
* **Advanced Mode UI:** A horizontal stepper wizard. Users define entity properties using structured dropdowns and text inputs, with real time relationship mapping visualization.
* **Pricing UI:** The pricing page now uses the shared logo treatment and includes an explicit header path back to the home page.
* **Generation State UI:** A real time progress indicator utilizing Supabase WebSockets. It displays detailed logs including "Injecting Handlebars Templates," "Generating C# Controllers," and "Running Compile Guarantee CI Check."

**3. API and Orchestration Design**
Next.js Server Actions manage the core logic to keep secrets secure.

* **Server Action: `GenerateSchemaFromPrompt(promptText)`**
    * Calls the LLM to translate natural language into the strict JSON schema required by the UI canvas.
* **Server Action: `SynthesizeArchitecture(generationId)`**
    * Fetches `payload_json`.
    * Executes the Handlebars compilation for static files.
    * Calls the LLM for dynamic file generation.
    * Reconstructs the directory.
* **Service: Build Validation**
    * Planned service that will run the compiler toolchain locally on the Proxmox staging server or the production worker node.
* **Service: File System & Storage**
    * Planned service that will compress the validated directory, upload to Cloudflare R2, and generate a 24 hour presigned URL.

**4. V1 to V2 Stack Matrix Management**
The UI includes a configuration panel before generation begins.
* **V1 Configuration:** Hardcoded to .NET (Backend), Dapper (ORM), Next.js (Frontend), PostgreSQL (Database).
* The Next.js backend uses this configuration state to route the generation request to the correct template library directory on the server, ensuring the Handlebars engine loads the correct base files.
