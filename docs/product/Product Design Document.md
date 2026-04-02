### Product Design Document (PDD): StackAlchemist

> Implementation status note (2026-04-02): the current codebase implements the landing page, Simple Mode mock flow, and Advanced Mode wizard from Phase 1. The server actions, real-time generation pipeline, checkout, and file delivery behaviors below remain design intent and are not fully wired end to end yet.

**1. System Overview**
StackAlchemist orchestrates code generation by connecting a Next.js frontend with a Supabase backend. It uses a dual mode intake flow and a hybrid template generation engine to compile custom business logic into a highly optimized .NET/Next.js repository, guaranteeing successful compilation before user delivery.

**2. UI/UX Design Specifications**
* **Theme:** Dark mode default. Slate gray backgrounds with electric blue interactive elements.
* **Component Library:** `shadcn/ui` styled with Tailwind CSS.
* **Simple Mode UI:** A large, focused text area resembling a command line interface. Upon submission, it transitions to a visual entity mapping canvas.
* **Advanced Mode UI:** A horizontal stepper wizard. Users define entity properties using structured dropdowns and text inputs, with real time relationship mapping visualization.
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
