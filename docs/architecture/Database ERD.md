# StackAlchemist: Database ERD

> **Status (2026-04-04):** Migration SQL files checked in at `supabase/migrations/`. Schema matches the TypeScript types in `src/StackAlchemist.Web/src/lib/types.ts`. RLS policies and Realtime publication are included.

This diagram illustrates the core relational structure within the Supabase PostgreSQL database.

```mermaid
erDiagram
    %% Entities
    profiles {
        uuid id PK "Matches auth.users.id"
        text email
        text api_key_override "Encrypted BYOK"
        text preferred_model "Default: claude-3-5-sonnet"
        timestamptz created_at
    }

    transactions {
        uuid id PK
        uuid user_id FK
        text stripe_session_id "Unique Stripe ID"
        int tier "0 (Spark), 1 (Blueprint), 2 (Boilerplate), 3 (IaC)"
        int amount "In cents"
        text status "pending, completed, failed, refunded"
        timestamptz created_at
    }

    generations {
        uuid id PK
        uuid user_id FK
        uuid transaction_id FK
        text mode "simple, advanced"
        int tier "0-3"
        text prompt
        jsonb schema_json "Extracted/user-defined schema"
        text status "pending, extracting_schema, generating_code, building, success, failed"
        text download_url "Presigned R2 URL"
        jsonb preview_files_json "Tier 0 only: inline file map"
        text build_log "Streaming build output"
        text error_message
        int attempt_count
        timestamptz created_at
        timestamptz updated_at
        timestamptz completed_at
    }

    %% Relationships
    profiles ||--o{ transactions : "makes"
    profiles ||--o{ generations : "owns"
    transactions ||--o| generations : "unlocks"
```

## RLS Policies

| Table | Policy | Rule |
|-------|--------|------|
| `profiles` | Users read/update own | `auth.uid() = id` |
| `transactions` | Users read own | `auth.uid() = user_id` |
| `transactions` | Service role manages | `auth.role() = 'service_role'` |
| `generations` | Anyone can insert | `true` (supports unauthenticated free tier) |
| `generations` | Anyone can read | `true` (supports Realtime subscriptions by ID) |
| `generations` | Service role updates | `auth.role() = 'service_role'` |

## Realtime

`generations` table is added to `supabase_realtime` publication for live status streaming to the frontend.
