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
| **Tier 0** | Spark | Free | Live preview only, with schema canvas and no download |
| **Tier 1** | Blueprint | $299 | Architecture documents: ER schema, OpenAPI spec, SQL migration scripts, data flow diagram |
| **Tier 2** | Boilerplate | $599 | Everything in Blueprint + compiled .NET 10 API, Next.js 15 frontend, PostgreSQL schema, Docker Compose |
| **Tier 3** | Infrastructure | $999 | Everything in Boilerplate + AWS CDK stack, Helm charts, CI/CD pipeline, deployment runbook |

> **All prices are one-time.** No subscriptions, no recurring fees. The generated code is entirely yours.

---

## Step 4: Generate

Click **Synthesize** (or press `Ctrl + Enter` in Simple Mode). Watch real-time progress as StackAlchemist:

1. Parses and validates your schema
2. Applies Handlebars templates to the structure
3. Injects LLM-generated business logic into the holes
4. Runs the output through the compiler
5. Auto-corrects any build errors (up to 3 retries)
6. Packages everything into a ZIP archive

Generation typically takes **30–90 seconds** depending on schema complexity.

---

## Step 5: Download and Run

Extract the ZIP archive. Every Boilerplate and Infrastructure package includes a `README.md` with specific setup instructions, but the general pattern is:

```bash
# 1. Unzip and navigate
cd your-project-name

# 2. Copy environment config
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

- Review `README.md` in the root — it has all env variable documentation
- The `.env.example` file lists all required configuration keys
- Database migrations are in `/migrations` — they run automatically on first `docker compose up`
- Supabase auth keys need to be populated from your Supabase project dashboard

---

## Next Steps

- [Using Simple Mode in depth →](./simple-mode)
- [Using the Advanced Mode entity wizard →](./advanced-mode)
- [Understanding tiers in detail →](./tiers-and-pricing)
- [What's in your download →](./your-output)
