# Generate a full AI Project Management SaaS from a prompt

You describe the kind of project management product your team needs. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI project management SaaS with:

- **Projects and workspaces** with multi-team scoping and per-project permissions
- **Tasks and subtasks** with assignees, due dates, priority, status, and dependencies
- **Sprints and milestones** with capacity planning and burn-down tracking
- **Kanban and list views** with drag-and-drop status changes and saved filters
- **Comments and mentions** on tasks with notification routing
- **Time tracking** with manual entry and timer-based capture
- **Reports and dashboards** for velocity, completion rate, and overdue task surfacing
- **Role-based access control** so contributors see assigned work, managers see the team, admins see everything
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

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
- API endpoints: `POST /tasks`, `PATCH /tasks/:id/status`, `POST /sprints/:id/start`, `GET /reports/velocity`, plus webhook receiver at `/api/webhooks/github` for PR auto-linking
- A Next.js kanban UI, sprint planning view, manager dashboard, and time-tracking interface

All wired into a Next.js frontend and a .NET backend with WebSocket-based live updates for the kanban board. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Connect your identity provider.** The generated repo defaults to Supabase Auth for email/password and Google OAuth. If your team uses Microsoft 365 or Okta, swap the auth provider — the middleware abstracts the JWT validation, and changing it is a single-file change. PM tools live or die on whether your team will log in daily; lean on the auth your team already uses.

2. **Wire the GitHub PR-link webhook.** The generated `/api/webhooks/github` endpoint includes PR-link parsing and signature verification, but you need to register the webhook on your GitHub org and drop the webhook secret into `.env`. Once connected, every PR branch matching `task-123/` auto-links the PR to task 123 and updates the task status when the PR merges. This is the single feature that makes engineering teams actually use the PM tool.

## What is not included

StackAlchemist is not Jira. We don't host your PM tool, don't ship a managed mobile app, and don't provide an enterprise app marketplace. We generate you the code. You deploy and operate it.

We don't include Gantt charts, dependency-aware critical path visualization, or resource-allocation forecasting out of the box — those are dense visualizations with their own engineering surface, best added once you know whether your team will actually use them. The PM tool you get is the daily-driver operational layer plus the reporting skeleton. Specialized visualizations are yours to add.

## Pricing

One-time, per generation:

- **Simple-mode PM SaaS** — $299. Single workspace, basic kanban, tasks + assignees, simple reports.
- **Blueprint-tier** — $599. Multi-team, sprints, dependencies, time tracking, RBAC, GitHub linking.
- **Boilerplate-tier** — $999. Multi-workspace, custom workflows per project, advanced reporting, webhook integrations for Slack / Linear / GitHub, audit log on all state changes.

No monthly fee. No per-seat tax. You own what you generate.

## Get started

Describe your PM tool in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
