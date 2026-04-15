# Implementation Plan - StackAlchemist Core Expansion & UI Refinement

This plan outlines the steps to harden the generation engine, expand template support to Python/React, and refine the Advanced Mode UI with real-time feedback.

## Phase 1: Core Engine Refinement (Multi-Ecosystem Support)
**Objective:** Transition from a .NET-only build system to a flexible multi-ecosystem architecture.

### Changes:
- **Models (`src/StackAlchemist.Engine/Models/GenerationModels.cs`):**
    - Add `ProjectType` enum (`DotNetNextJs`, `PythonReact`, etc.) to `GenerationContext` and `GenerateRequest`.
- **Compile Service (`src/StackAlchemist.Engine/Services/CompileService.cs`):**
    - Refactor `ExecuteBuildAsync` to use a Strategy pattern or a simple switch based on `ProjectType`.
    - Implement `ExecutePythonBuildAsync` (venv setup + lint check) and `ExecuteNpmBuildAsync`.
- **Worker (`src/StackAlchemist.Engine/Services/CompileWorkerService.cs`):**
    - Ensure the LLM retry loop passes the correct ecosystem-specific error context.

## Phase 2: Template Expansion (Python-React)
**Objective:** Add a second high-quality template to verify multi-ecosystem support.

### Changes:
- **Templates (`src/StackAlchemist.Templates/V1-Python-React`):**
    - Create a new template directory.
    - Implement a FastAPI backend with SQLAlchemy models.
    - Implement a React (Vite) frontend with Tailwind.
- **Template Provider:** Update `TemplateProvider.cs` to resolve the new path.

## Phase 3: Advanced Mode UI & Real-time Logs
**Objective:** Finish the Advanced Mode wizard and show live compilation logs to the user.

### Changes:
- **Frontend (`src/StackAlchemist.Web/src/app/advanced`):**
    - Add "Project Type" selection to Step 2 of the wizard.
    - Enhance `AdvancedModePage.tsx` to subscribe to the `build_logs` table in Supabase.
    - Implement a "Terminal" component to display these logs during the `Building` state.
- **Server Actions (`src/StackAlchemist.Web/src/lib/actions.ts`):**
    - Update `submitAdvancedGeneration` to include the selected `ProjectType`.

## Phase 4: Testing & QA (The "Full Circuit")
**Objective:** Verify the entire pipeline from prompt to zip upload.

### Changes:
- **Integration Tests (`src/StackAlchemist.Engine.Tests/Integration`):**
    - Create `MultiEcosystemPipelineTests.cs`.
    - Mock the LLM but run real `dotnet` and `npm` build commands in temporary directories.
- **E2E Tests:** Add a Playwright test that walks through the Advanced Mode wizard.

## Phase 5: Infrastructure Validation (Last)
**Objective:** Final sanity check of the production/test environment.

### Steps:
- Verify GitHub Secrets are mapped correctly in `.github/workflows/deploy-test.yml`.
- Run a manual trigger of the `deploy-test` workflow.
- Confirm R2 bucket policies allow the engine to upload zips.

## Verification Plan
1. **Unit:** `dotnet test` specifically targeting the new `CompileService` strategies.
2. **Integration:** Run the "Full Circuit" test for both .NET and Python templates.
3. **Manual:** Generate a "Simple" .NET project and an "Advanced" Python project on the test site.
