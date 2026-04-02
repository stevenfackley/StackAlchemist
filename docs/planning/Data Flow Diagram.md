# StackAlchemist: Architecture & Data Flow Diagram

```mermaid
graph TD
    %% Styling
    classDef user fill:#1E293B,stroke:#3B82F6,stroke-width:2px,color:#fff;
    classDef gateway fill:#1E293B,stroke:#8B5CF6,stroke-width:2px,color:#fff;
    classDef db fill:#0F172A,stroke:#10B981,stroke-width:2px,color:#fff;
    classDef engine fill:#1E293B,stroke:#F43F5E,stroke-width:2px,color:#fff;
    classDef external fill:#0F172A,stroke:#F59E0B,stroke-width:2px,color:#fff;
    classDef storage fill:#0F172A,stroke:#F97316,stroke-width:2px,color:#fff;

    %% Entities
    User((User)):::user
    Stripe((Stripe API)):::external
    Claude((Claude 3.5 Sonnet)):::external

    %% Next.js Gateway
    subgraph Frontend ["Next.js 15 (Web & API Gateway)"]
        UI[Simple/Advanced UI]:::gateway
        Checkout[Checkout Service]:::gateway
        Orchestrator[Orchestration API]:::gateway
    end

    %% Database
    subgraph Supabase ["Supabase PostgreSQL"]
        Profiles[(profiles)]:::db
        Transactions[(transactions)]:::db
        Generations[(generations)]:::db
    end

    %% Core Engine & Worker
    subgraph Backend [".NET 10 Generation Platform"]
        Templates[Template Provider]:::engine
        Reconstruction[Reconstruction Engine]:::engine
        Worker[Compile Guarantee Worker]:::engine
    end

    %% Storage
    R2[(Cloudflare R2)]:::storage

    %% Workflows
    User -->|1. Prompts / Schema UI| UI
    UI -->|2. Validates & Sends| Orchestrator
    Orchestrator -->|3. Save Initial State| Generations

    %% Payment Flow
    UI -->|4. Selects Tier| Checkout
    Checkout -->|5. Payment Intent| Stripe
    Stripe -->|6. Webhook Status| Transactions

    %% Generation Flow
    Orchestrator -->|7. Trigger Generation| Reconstruction
    Reconstruction -->|8. Fetch Base Handlebars| Templates
    Reconstruction -->|9. Send Schema & RAG Context| Claude
    Claude -->|10. Delimited C#/TS Code Blocks| Reconstruction
    
    %% Compile Guarantee Flow
    Reconstruction -->|11. Output Reconstructed Dir| Worker
    Worker -->|12. Run dotnet build & npm build| Worker
    Worker -.->|12a. ERROR: Capture stderr| Claude
    Claude -.->|12b. Surgical Fixes| Worker
    
    %% Delivery Flow
    Worker -->|13. SUCCESS: Zip Directory| R2
    R2 -->|14. Return Presigned URL| Orchestrator
    Orchestrator -->|15. Broadcast Success| UI
    UI -->|16. Download .zip| User
```

### Flow Breakdown
1. **Intake:** The user submits a prompt or visual schema via the Next.js UI.
2. **Checkout:** The user selects a tier. Stripe processes the payment and a webhook updates the Supabase `transactions` table.
3. **Generation:** The .NET Engine loads static Master Templates, sends the database schema to Claude 3.5, and receives delimited code blocks in return.
4. **Reconstruction:** The engine merges the static Handlebars templates with the dynamic LLM code and writes it to a temporary directory.
5. **Compile Guarantee:** The worker executes CLI build commands. If it fails, it loops back to Claude for automated fixes.
6. **Delivery:** Upon a successful exit code (0), the directory is zipped, uploaded to Cloudflare R2, and a presigned URL is streamed back to the user via WebSockets.
