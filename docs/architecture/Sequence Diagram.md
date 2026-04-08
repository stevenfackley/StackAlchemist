# StackAlchemist: Generation Sequence Diagram

This diagram illustrates the chronological execution of a StackAlchemist generation, including asynchronous Stripe webhooks and real-time WebSocket communication.

```mermaid
sequenceDiagram
    autonumber
    
    actor User
    participant Web as Next.js (Frontend)
    participant API as Next.js (Server Actions)
    participant DB as Supabase DB
    participant Stripe as Stripe API
    participant Engine as .NET Generation Engine
    participant LLM as Claude 3.5 API
    participant Worker as Build Worker (IBuildStrategy)
    participant R2 as Cloudflare R2
    
    %% INTAKE & CHECKOUT
    User->>Web: Submits Prompt / Schema
    Web->>API: GenerateSchemaFromPrompt()
    API->>LLM: Request JSON Schema
    LLM-->>API: Return JSON Schema
    API-->>Web: Render Node UI
    User->>Web: Confirms Schema
    User->>Web: Completes Personalization Wizard (optional)
    User->>Web: Selects Platform & Tier ($599)
    Web->>Engine: POST /api/stripe/create-session
    Engine->>Stripe: Create Checkout Session
    Stripe-->>Engine: Session URL
    Engine-->>Web: Return Checkout URL
    Stripe-->>Web: Return Checkout URL
    User->>Stripe: Completes Payment
    
    %% ASYNC WEBHOOK & INITIALIZATION
    Stripe-)API: Webhook (checkout.session.completed)
    API->>DB: Insert Transaction & Init Generation (Status: PENDING)
    API->>Engine: Trigger SynthesizeArchitecture(gen_id)
    
    %% GENERATION PROCESS (With WebSockets)
    par Real-time Updates
        DB-)Web: Broadcast Status via WebSockets
    and Engine Execution
        Engine->>DB: Update Status (Status: GENERATING)
        Engine->>Engine: Load Master Handlebars Templates
        Engine->>LLM: Send Context + Schema
        LLM-->>Engine: Delimited Code Blocks
        Engine->>Engine: Parse & Hydrate Files
        Engine->>Worker: Dispatch to Compile Guarantee
    end
    
    %% COMPILE GUARANTEE LOOP
    Worker->>DB: Update Status (Status: BUILDING)
    loop Max 3 Retries
        Worker->>Worker: Run build via IBuildStrategy (dotnet or pip+npm)
        alt Exit Code 1 (Error)
            Worker->>LLM: Send `stderr` for correction
            LLM-->>Worker: Return patched code blocks
        else Exit Code 0 (Success)
            Worker->>Worker: Break Loop
        end
    end
    
    %% PACKING & DELIVERY
    Worker->>DB: Update Status (Status: PACKING)
    Worker->>R2: Upload zipped directory
    R2-->>Worker: Return Object Key
    Worker->>DB: Update Generation Record (Status: SUCCESS, R2_Key)
    DB-)Web: Broadcast Success & UI Refresh
    Web->>API: Request Presigned URL
    API->>R2: Generate Presigned URL (168hr prod / 24hr dev)
    R2-->>API: URL
    API-->>Web: URL
    Web-->>User: Display Download Button
```
