# Understanding Your Output

When generation completes, you receive a ZIP archive containing your complete project. This page explains what's inside, how it's organized, and how to get it running.

---

## Archive Structure

Every Boilerplate and Infrastructure package follows the same directory layout:

```
your-project-name/
├── README.md                    ← Project-specific setup instructions
├── .env.example                 ← All required environment variables
├── docker-compose.yml           ← Full-stack local dev environment
├── docker-compose.prod.yml      ← Production-ready compose (Tier 3)
│
├── api/                         ← .NET 10 Web API
│   ├── YourProject.Api/         ← Controllers, middleware, program entry
│   ├── YourProject.Core/        ← Domain models, interfaces
│   ├── YourProject.Data/        ← Repositories, migrations, Dapper queries
│   └── YourProject.sln          ← Solution file
│
├── web/                         ← Next.js 15 frontend
│   ├── src/
│   │   ├── app/                 ← App Router pages and layouts
│   │   ├── components/          ← UI components
│   │   ├── lib/                 ← API client, utilities, types
│   │   └── types/               ← Generated TypeScript interfaces
│   ├── package.json
│   └── next.config.ts
│
├── migrations/                  ← Ordered SQL migration files
│   ├── 001_initial_schema.sql
│   ├── 002_indexes.sql
│   └── 003_rls_policies.sql
│
└── infra/                       ← Tier 3 only: Cloud infrastructure
    ├── cdk/                     ← AWS CDK TypeScript stack
    ├── helm/                    ← Kubernetes Helm charts
    └── .github/workflows/       ← GitHub Actions CI/CD
```

---

## The API Project (.NET 10)

### Project Structure

The API follows a clean architecture pattern with three layers:

**`YourProject.Api`** — HTTP layer
- `Controllers/` — One controller per entity, with all specified endpoints
- `Program.cs` — Service registration, middleware pipeline, Supabase auth config
- `Middleware/` — Error handling, request logging

**`YourProject.Core`** — Domain layer
- `Models/` — C# record types for each entity
- `Interfaces/` — Repository and service interfaces
- `Enums/` — All enum types (status values, role types, etc.)

**`YourProject.Data`** — Data layer
- `Repositories/` — Dapper implementations for each entity
- `Migrations/` — EF-style migration runner (SQL files, not EF Core)
- `DbContext.cs` — Dapper connection factory

### Naming Conventions

Generated code follows standard .NET conventions:
- Classes: PascalCase (`ProjectTask`, not `projectTask`)
- Properties: PascalCase (`DueDate`, not `due_date`)
- Database columns: snake_case (via Dapper column mapping)
- Namespaces: `YourProjectName.Layer.Sublayer`

### Authentication

API endpoints are secured via Supabase JWT validation. The `[Authorize]` attribute is applied automatically to all non-public endpoints. The `Program.cs` includes:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.Authority = supabaseUrl;
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuerSigningKey = true,
            ValidAudience = "authenticated"
        };
    });
```

---

## The Frontend (Next.js 15)

### App Router Structure

```
src/app/
├── layout.tsx           ← Root layout with auth provider
├── page.tsx             ← Landing/home page
├── (auth)/              ← Login, sign up, forgot password flows
│   ├── login/
│   └── signup/
├── (dashboard)/         ← Authenticated area
│   ├── layout.tsx       ← Dashboard shell (sidebar, nav)
│   └── [entity]/        ← CRUD pages per entity
│       ├── page.tsx     ← List view
│       ├── [id]/page.tsx ← Detail/edit view
│       └── new/page.tsx ← Create form
└── api/                 ← Next.js API routes (thin proxy to .NET API)
```

### Generated API Client

A fully typed API client is generated at `src/lib/api.ts`:

```typescript
// Auto-generated — do not edit manually
export const api = {
  projects: {
    list: () => fetch('/api/projects').then(r => r.json()),
    get: (id: string) => fetch(`/api/projects/${id}`).then(r => r.json()),
    create: (data: CreateProjectInput) => fetch('/api/projects', {
      method: 'POST',
      body: JSON.stringify(data)
    }).then(r => r.json()),
    // ...
  }
}
```

### TypeScript Types

All entity interfaces are generated at `src/types/`:

```typescript
export interface Project {
  id: string;
  name: string;
  description: string | null;
  status: 'active' | 'archived' | 'draft';
  ownerId: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProjectInput {
  name: string;
  description?: string;
  status?: Project['status'];
}
```

---

## Migrations

SQL migrations are in `/migrations`, numbered sequentially. They run automatically on `docker compose up` via the init script.

**To run manually:**

```bash
# Connect to the database
psql postgresql://postgres:password@localhost:5432/yourdb

# Run a specific migration
\i migrations/001_initial_schema.sql
```

Each migration file is idempotent — it checks for existing objects before creating them.

---

## Environment Variables

Copy `.env.example` to `.env` and fill in the required values:

```env
# Database
DATABASE_URL=postgresql://postgres:password@localhost:5432/yourdb

# Supabase (get these from your Supabase project dashboard)
NEXT_PUBLIC_SUPABASE_URL=https://your-project.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=your-anon-key
SUPABASE_SERVICE_ROLE_KEY=your-service-role-key

# API
API_BASE_URL=http://localhost:5000
NEXT_PUBLIC_API_URL=http://localhost:5000
```

---

## Running Locally

### Prerequisites

- Docker Desktop
- (Optional) .NET 10 SDK if you want to run the API outside Docker
- (Optional) Node.js 20+ if you want to run the frontend outside Docker

### Quick Start

```bash
# Start everything
docker compose up

# Or rebuild after code changes
docker compose up --build
```

Services will be available at:
| Service | URL |
|---------|-----|
| Frontend | http://localhost:3000 |
| API | http://localhost:5000 |
| API Swagger | http://localhost:5000/swagger |
| PostgreSQL | localhost:5432 |

### Running Services Individually

```bash
# API only
cd api
dotnet run --project YourProject.Api

# Frontend only
cd web
npm install
npm run dev
```

---

## Blueprint-Only Output (Tier 1)

If you purchased the Blueprint tier, your archive contains:

```
your-project-name/
├── schema.json            ← Full entity-relationship schema
├── openapi.yaml           ← OpenAPI 3.0 spec (importable into Postman/Insomnia)
├── migrations/            ← SQL DDL scripts
├── data-flow-diagram.md   ← Mermaid diagram of data flows
└── adrs/                  ← Architecture Decision Records
    ├── 001-database-choice.md
    ├── 002-api-pattern.md
    └── 003-auth-approach.md
```

---

## Related Docs

- [Getting Started →](./getting-started)
- [Tiers and Pricing →](./tiers-and-pricing)
- [Troubleshooting →](./troubleshooting)
