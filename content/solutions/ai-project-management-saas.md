# Generate a full AI Project Management SaaS from a prompt

You describe the kind of project management product your team needs. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI project management SaaS with:

- **Workspaces and projects** — `Workspace`, `Project` and `Team` entities with team scoping
- **Tasks and subtasks** — `Task`, `Subtask` and `TaskDependency` entities with assignees, due dates, priority, status and estimates
- **Sprints and milestones** — `Sprint` and `Milestone` entities with dates and status
- **Comments and mentions** — `Comment` and `Mention` entities on tasks and projects
- **Time tracking** — `TimeEntry` and `Timer` entities
- **Members and roles** — `User`, `Role` and `Membership` entities, the data per-project permissions are built on
- **Notifications** — `Notification` and `NotificationChannel` entities
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** sign-in and permission checks (the Supabase client is preinstalled; the auth flows are yours to write), the kanban and list views with drag-and-drop, burndown and velocity reports, notification delivery, live updates, the GitHub integration, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

Generation takes about 12 minutes from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of using Jira or Asana

**Generic PM tools are everybody's PM tool and nobody's.** Jira is configured for enterprise software teams and feels heavy for everyone else. Asana is configured for marketing teams and feels weird for engineering. Monday is configured for anything and committed to nothing. A generated PM tool is configured for your team's actual workflow from the prompt up.

**Per-seat pricing taxes you for growth.** A 50-person team on Jira is $10,000+/year. A generated PM tool is paid for once and runs on your infrastructure with zero per-seat tax. The economics flip the moment your team is more than ten people.

**Vendor PM tools control your workflow data.** Your sprint history, your velocity metrics, your team's complete work record sit in someone else's database. Migrating off Jira to a custom tool later is a 6-month engineering project. Owning the code from day one means there's no migration.

## Who this is for

- **Engineering team leads at startups** who want a real PM tool but refuse to pay enterprise seat pricing.
- **Vertical-specific SaaS founders** building PM tools for a specific industry (legal cases, construction projects, video production schedules) where generic tools don't fit.
- **Agencies** delivering custom client-portals with PM functionality (client sees their projects, agency sees all clients).
- **Internal-tools teams** at established companies who want a PM tool tightly integrated with their existing identity and reporting infrastructure.

## Example entities generated

A typical AI Project Management SaaS generation produces entities like:

- `Workspace` / `Project` / `Team`
- `Task` / `Subtask` / `TaskDependency`
- `Sprint` / `Milestone`
- `Comment` / `Mention`
- `TimeEntry` / `Timer`
- `User` / `Role` / `Membership`
- `Notification` / `NotificationChannel`

The exact shape depends on your prompt. An engineering team generates different entities than a marketing operations team.

### Real example: Software engineering team

Imagine you submit this spec:

> "We run a 30-person engineering team across 5 squads. Each squad has its own kanban board with stages: backlog, in-progress, in-review, done. Tasks have story-point estimates, assignees, and can depend on other tasks. We run two-week sprints and want a burndown view. Engineers can log time against tasks. Managers see velocity and overdue counts per squad. We use GitHub and want PRs to auto-link tasks when the branch name contains the task ID."

StackAlchemist generates:

- `Workspace` entity with name, default_sprint_length_days, default_estimation_unit
- `Squad` (Team) entity with name, lead_user_id, color
- `Project` entity with workspace_id, squad_id, name, description, archived flag
- `Task` entity with project_id, title, description, assignee_id, status, story_points, sprint_id, dependency_ids (array), order (for kanban position)
- `Sprint` entity with workspace_id, squad_id, name, start_date, end_date, status
- `TimeEntry` entity with task_id, user_id, duration_minutes, logged_at, notes
- `Comment` entity polymorphic over task / project, with mentions[] for notifications
- `GitHubLink` entity with task_id, pr_url, pr_state, linked_via (branch-name, manual)
- CRUD endpoints for every entity (`/api/v1/tasks`, `/api/v1/sprints`, `/api/v1/projects`, …). Endpoints like `PATCH /tasks/:id/status`, `GET /reports/velocity` or a GitHub webhook receiver at `/api/webhooks/github` can be declared in Advanced Mode; the logic behind them is yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the PM tool's data model and CRUD layer, compile-verified. The kanban UI with WebSocket live updates, the sprint planning and burndown views, the manager dashboard and the time-tracking interface are code you build on it. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Add sign-in with the identity provider your team already uses.** The generated repo has no authentication: the Supabase client is preinstalled in the frontend, but no sign-in flow or JWT validation is written. Use Supabase Auth, or Microsoft 365 or Okta if that is where your team lives, add JWT validation to the .NET API, and scope projects and tasks to the signed-in user's memberships. PM tools live or die on whether your team will log in daily; lean on the auth your team already uses.

2. **Build the GitHub PR-link webhook.** The repo gives you the `GitHubLink` entity; the webhook endpoint, its signature verification, and the branch-name parsing are yours to write. Register the webhook on your GitHub org, drop the webhook secret into `.env`, and make every PR branch matching `task-123/` link the PR to task 123 and update the task status when the PR merges. This is the single feature that makes engineering teams actually use the PM tool.

## What is not included

StackAlchemist is not Jira. We don't host your PM tool, don't ship a managed mobile app, and don't provide an enterprise app marketplace. We generate you the code. You deploy and operate it.

We don't include Gantt charts, dependency-aware critical path visualization, or resource-allocation forecasting out of the box — those are dense visualizations with their own engineering surface, best added once you know whether your team will actually use them. The PM tool you get is the data model and CRUD layer your daily-driver views are built on. Specialized visualizations are yours to add.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No per-seat tax. You own what you generate.

## Get started

Describe your PM tool in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
