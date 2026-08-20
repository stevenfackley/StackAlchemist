# Getting Started with StackAlchemist

StackAlchemist turns a natural language description of your SaaS into a fully compiled, production-ready codebase — backed by a **compile guarantee**. This guide walks you from first visit to downloaded archive in under five minutes.

---

## What You Need

- A browser. That's it.
- No local tooling required to generate an architecture.
- An account is required only at the point of purchase/download.

---

## Step 1: Choose Your Input Mode

From the main console, select how you want to define your application:

| Mode | Best For |
|------|----------|
| **Simple Mode** | You have an idea but haven't modeled entities yet. Describe in plain English. |
| **Advanced Mode** | You already know your data model. Use the visual entity wizard for precision. |

You can switch modes freely before submitting.

---

## Step 2: Describe Your Application

### Simple Mode

Type a description of your SaaS in the prompt field. The more specific you are about entities and relationships, the better your output will be.

**Good prompt example:**
```
A subscription-based project management SaaS. Users belong to organizations.
Organizations have projects. Projects have tasks with assignees, due dates,
priority levels, and status. Tasks can have comments and file attachments.
Projects have a Kanban board view and a list view.
```

**What to include:**
- Your main data entities (Users, Projects, Tasks, etc.)
- Relationships between them (belongs to, has many, etc.)
- Key features or workflows you need

### Advanced Mode

Use the entity wizard to define your schema step by step:

1. **Entities** — Add each data entity with its fields and field types
2. **Relationships** — Define how entities relate to each other
3. **API Endpoints** — Specify the REST endpoints you need per entity
4. **Review** — Confirm the schema before generation

---

## Step 3: Select a Generation Tier

| Tier | Name | Price | What You Receive |
|------|------|-------|-----------------|
| **Tier 0** | Spark | Free | A fixed demo app that boots in your browser. Not built from your description, and not downloadable. |
| **Tier 1** | Blueprint | $299 | Architecture documents: `schema.json` and `api-docs.md` (the CRUD contract per entity) |
| **Tier 2** | Boilerplate | $599 | The generated source: .NET 10 minimal API, Next.js 16 frontend, PostgreSQL migration, Docker Compose — both halves compiled before delivery |
| **Tier 3** | Infrastructure | $999 | Everything in Boilerplate + AWS CDK stack, Terraform baseline, Helm chart, deployment runbook |

> **All prices are one-time.** No subscriptions, no recurring fees. The generated code is entirely yours.

### About the free tier

Spark exists so you can watch the machine run before paying for it. It renders one fixed
template — a small task tracker — with your project name substituted in, and makes **no AI
call at all**. That is why it is instant, free, and always boots. It is not a preview of the
code a paid tier would produce for you, it contains no .NET half, and it cannot be
downloaded.

What Spark is genuinely good for:

- Proving the end-to-end flow works in your browser before you pay (the in-browser runtime
  needs a Chromium-based browser — Chrome, Edge, Arc)
- Seeing the delivery page and the embedded editor exactly as a paid run presents them
- Reading a real Next.js 16 App Router project file by file, and editing it live
- Modelling your entities on the Advanced Mode ER canvas — that schema is saved with the
  build, so you can come back and buy a paid run against it

You get **5 free builds per calendar month** per account.

---

## Step 4: Generate

Click **Synthesize** (or press `Ctrl + Enter` in Simple Mode). Watch real-time progress as StackAlchemist:

1. Renders the template tree for your stack and substitutes your project name
2. Sends your description or schema to the model and reconstructs the returned files into that tree
3. Runs `dotnet restore` and `dotnet build` against the API half
4. Runs `npm ci`, `npm run typecheck`, and `next build` against the frontend half
5. Auto-corrects any build errors and retries (up to 3 retries)
6. Writes `build-report.json` and packages everything into a ZIP archive

Generation typically takes **30–90 seconds** depending on schema complexity.

A free Spark build skips all of that: it renders the fixed demo template and hands it to the
in-browser runtime, which is why it returns almost immediately.

---

## Step 5: Download and Run

Extract the ZIP archive. The root of a Boilerplate or Infrastructure package holds
`docker-compose.yml`, a multi-stage `Dockerfile`, `.env.example`, and `build-report.json`
(the record of the builds that were run against your code). The two halves live in
`dotnet/` and `nextjs/`.

> **Prerequisite — configure `.env` before starting the stack.** The API will not
> boot with an empty `.env`: after copying `.env.example`, open `.env` and fill in
> the required values — at minimum `DATABASE_URL` /
> `ConnectionStrings__DefaultConnection`. The Supabase entries are placeholders for
> the client library that ships preinstalled in the frontend; no auth flow is
> generated, so leave them alone until you wire one up. Skipping the database
> values is the #1 cause of "the frontend loads but every API call fails" on a
> fresh download.

```bash
# 1. Unzip and navigate
cd your-project-name

# 2. Copy environment config — then EDIT .env before continuing (see above)
cp .env.example .env

# 3. Start the full stack (database, API, frontend)
docker compose up
```

Your dev environment will be running at:
- **API:** `http://localhost:5000`
- **Frontend:** `http://localhost:3000`
- **Database:** `localhost:5432`

---

## Common First Steps After Download

- Read `build-report.json` in the root — it names every command that was run against your
  code, its exit code, and the per-half verdict
- The `.env.example` file lists all required configuration keys
- Database migrations are in `dotnet/Migrations/` — `docker compose up` mounts that directory
  into the Postgres init hook, so they run automatically on first boot
- API endpoints are mapped under `/api/v1/{entity}` and the OpenAPI document is served in
  Development at `/openapi/v1.json`

---

## Next Steps

- [Using Simple Mode in depth →](./simple-mode)
- [Using the Advanced Mode entity wizard →](./advanced-mode)
- [Understanding tiers in detail →](./tiers-and-pricing)
- [What's in your download →](./your-output)
