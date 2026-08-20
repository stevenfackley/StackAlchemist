# Understanding Your Output

When a **paid** generation completes, you receive a ZIP archive containing your project. This
page explains what's inside, how it's organized, and how to get it running.

> The free Spark tier produces no archive. It renders a fixed demo project into your browser
> and nothing is downloadable at that tier — see [Getting Started](./getting-started#about-the-free-tier).

---

## Archive Structure

Every Boilerplate and Infrastructure package follows the same directory layout. There are
exactly two top-level source directories — `dotnet/` and `nextjs/` — because the generation
prompt rejects any file path outside them:

```
your-project-name/
├── .env.example                 ← All required environment variables
├── .dockerignore
├── .gitignore
├── build-report.json            ← Every build command run against your code + verdict
├── docker-compose.yml           ← db + engine + web, one command
├── Dockerfile                   ← Multi-stage, two targets: `web` and `engine`
│
├── dotnet/                      ← .NET 10 minimal API (ONE project, not a solution)
│   ├── YourProject.csproj
│   ├── Program.cs               ← Serilog, CORS, Dapper factory, DI + route registrations
│   ├── Models/                  ← One record + Create…Request per entity
│   ├── Repositories/            ← Interface + Dapper implementation per entity
│   ├── Controllers/             ← One `…Endpoints.cs` per entity (MapGroup extensions)
│   ├── Infrastructure/          ← DbConnectionFactory
│   ├── Migrations/
│   │   └── 001_initial_schema.sql
│   ├── appsettings.json
│   └── appsettings.Development.json
│
├── nextjs/                      ← Next.js 16 frontend
│   ├── src/
│   │   ├── app/                 ← layout.tsx, page.tsx, globals.css
│   │   ├── lib/api.ts           ← Typed API client
│   │   └── types/index.ts       ← Generated TypeScript interfaces
│   ├── package.json             ← Includes `typecheck` (tsc --noEmit)
│   ├── next.config.ts
│   ├── tailwind.config.ts
│   ├── postcss.config.mjs
│   ├── eslint.config.mjs
│   └── tsconfig.json
│
└── infra/                       ← Tier 3 only: cloud infrastructure
    ├── cdk/                     ← AWS CDK TypeScript stack
    ├── terraform/               ← Terraform AWS baseline
    └── helm/                    ← Kubernetes chart
```

Tier 3 also adds `DEPLOYMENT.md` at the root.

> **Not in the archive:** there is no `README.md`, no solution file, no
> `docker-compose.prod.yml`, and no GitHub Actions workflow. The setup instructions are this
> page; the layout above is the whole of it.

---

## The API Project (.NET 10)

### Project Structure

The API is a **single ASP.NET Core project** using minimal APIs — one `.csproj`, no solution
file, no Api/Core/Data split. Folders inside it separate concerns by namespace:

**`Models/`** — one file per entity: the `record` plus its `Create{Entity}Request` DTO
**`Repositories/`** — one file per entity: `I{Entity}Repository` plus a Dapper implementation
with `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`
**`Controllers/`** — one `{Entity}Endpoints.cs` per entity: a static class exposing
`Map{Entity}Endpoints(this WebApplication app)` that maps the CRUD `MapGroup`
**`Infrastructure/`** — `DbConnectionFactory` (Npgsql), injected everywhere a connection is needed
**`Program.cs`** — Serilog, CORS from `Cors:AllowedOrigins`, the connection factory, OpenAPI
in Development, and the generated DI + route registration lines

Endpoints are mapped at `/api/v1/{entity}s`. In Development the OpenAPI document is served at
`/openapi/v1.json` by `MapOpenApi()`.

### Naming Conventions

Generated code follows standard .NET conventions:
- Classes: PascalCase (`ProjectTask`, not `projectTask`)
- Properties: PascalCase (`DueDate`, not `due_date`)
- Tables: lowercase plural of the entity (`Customer` → `customers`); columns snake_case
- Namespaces: `YourProjectName.Models`, `.Repositories`, `.Controllers`, `.Infrastructure`
- IDs are `Guid` in C# and `string` in TypeScript

### Authentication

**No authentication is generated.** The endpoints are open and there is no `[Authorize]`
attribute, no JWT bearer configuration, and no login flow. What ships is the groundwork:
`@supabase/supabase-js` is a dependency of the frontend, and `.env.example` plus
`docker-compose.yml` carry the `NEXT_PUBLIC_SUPABASE_*` and `SUPABASE_SERVICE_ROLE_KEY`
slots. The generated migration enables row-level security on every table
(`ALTER TABLE … ENABLE ROW LEVEL SECURITY`) but writes no policies, so a table is closed to
the anon role until you add them.

Wiring auth is your first real task on top of the archive, and it is deliberate: which
provider you use, and whose claims your RLS policies trust, is not a decision worth guessing
on your behalf.

---

## The Frontend (Next.js 16)

### App Router Structure

The frontend is deliberately small — a working, typed shell over your API, not a finished
product UI:

```
nextjs/src/
├── app/
│   ├── layout.tsx       ← Root layout
│   ├── page.tsx         ← Home page, linking to each entity's endpoint
│   └── globals.css      ← Tailwind entry
├── lib/
│   └── api.ts           ← Typed API client (apiFetch + per-entity helpers)
└── types/
    └── index.ts         ← One interface + Create…Input type per entity
```

There are **no route groups, no auth pages, and no per-entity CRUD pages**. Nothing is
scaffolded under `(auth)/` or `(dashboard)/`, and there is no Next.js API-route proxy —
`lib/api.ts` calls the .NET API directly at `NEXT_PUBLIC_API_URL`. Building the screens is
your work; the types, the client, and a build that passes `tsc --noEmit` are ours.

### Generated API Client

A fully typed API client is generated at `nextjs/src/lib/api.ts` (the frontend half of
the archive — paths are relative to `nextjs/`):

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

All entity interfaces are generated in `nextjs/src/types/index.ts`:

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

The schema ships as a single file, `dotnet/Migrations/001_initial_schema.sql`: the
`uuid-ossp` extension, one `CREATE TABLE` per entity with UUID primary keys and foreign keys,
and `ALTER TABLE … ENABLE ROW LEVEL SECURITY` on each.

`docker-compose.yml` mounts that directory read-only into the Postgres container's
`/docker-entrypoint-initdb.d`, so it runs **once, on first boot of an empty volume**. Drop the
volume (`docker compose down -v`) to re-run it.

**To run manually:**

```bash
psql postgresql://postgres:postgres@localhost:5432/yourdb -f dotnet/Migrations/001_initial_schema.sql
```

---

## Environment Variables

Copy `.env.example` to `.env` and fill in the required values:

```env
# Database
DATABASE_URL=postgresql://postgres:postgres@localhost:54322/yourproject
ConnectionStrings__DefaultConnection=${DATABASE_URL}

# .NET engine
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:5000

# Next.js frontend
NEXT_PUBLIC_API_URL=http://localhost:5000

# Supabase — placeholders for the preinstalled client library. Nothing reads
# these until you wire up auth yourself.
NEXT_PUBLIC_SUPABASE_URL=https://your-project.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=your-anon-key
SUPABASE_SERVICE_ROLE_KEY=your-service-role-key
```

`docker compose` supplies its own database credentials for the containerised stack
(`postgres:postgres` against the `db` service); the `DATABASE_URL` above is for running the
API on the host.

---

## Running Locally

### Prerequisites

- Docker Desktop
- (Optional) .NET 10 SDK if you want to run the API outside Docker
- (Optional) Node.js 20+ if you want to run the frontend outside Docker (the images use
  Node 24)

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
| OpenAPI document (Development only) | http://localhost:5000/openapi/v1.json |
| PostgreSQL | localhost:5432 |

The `Dockerfile` has two targets and `docker-compose.yml` builds both from the project root:
`--target web` (Next.js standalone output on Node 24 Alpine) and `--target engine`
(`dotnet publish` onto the ASP.NET 10 runtime image). Both targets are built on every change
to the template in our own CI, so `docker compose up --build` is a supported first command,
not a hopeful one.

### Running Services Individually

```bash
# API only
cd dotnet
dotnet run

# Frontend only
cd nextjs
npm ci
npm run dev
```

---

## Blueprint-Only Output (Tier 1)

The Blueprint tier is documentation, not code. Its archive contains exactly two files:

```
your-project-name/
├── schema.json            ← Your entity-relationship schema, normalized and pretty-printed
└── api-docs.md            ← The CRUD contract: fields, types, keys, and the five
                             REST endpoints per entity, plus the relationship list
```

Nothing in a Blueprint is compiled, so a Blueprint carries no `build-report.json` and is not
sold under the Compile Guarantee. If you need the SQL, the migration is generated at
Boilerplate — the Blueprint gives you the model to write it from.

---

## Related Docs

- [Getting Started →](./getting-started)
- [Tiers and Pricing →](./tiers-and-pricing)
- [Troubleshooting →](./troubleshooting)
