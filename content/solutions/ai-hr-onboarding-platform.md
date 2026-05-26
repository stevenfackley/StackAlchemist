# Generate a full AI HR Onboarding Platform from a prompt

You describe how your company onboards new hires. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI HR onboarding platform with:

- **Employee records** with role, department, manager, start date, and employment status
- **Onboarding checklists** scoped per role with day-1, week-1, and month-1 templates
- **Document upload and storage** for I-9, W-4, offer letters, NDAs, and direct-deposit forms
- **E-signature scaffolding** with signed-document audit trail (DocuSign or HelloSign adapter)
- **Org chart** rendered from manager assignments with drill-down by department
- **Equipment and access requests** routed to IT with status tracking and approval workflow
- **Role-based onboarding flows** so engineering hires get different tasks than sales hires
- **Manager dashboard** showing pending tasks for each direct report and onboarding completion percentage
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

## Why generate it instead of paying BambooHR per seat

**Per-seat HR SaaS punishes growth.** BambooHR starts around $108 per employee per year. Hit 200 employees and you are paying north of $21,000 annually for software you do not own. A StackAlchemist-generated platform is one-time. Your headcount can quadruple and your HR tooling cost stays flat.

**Rippling locks you into a bundle.** You want onboarding, but Rippling wants you on their payroll, benefits, device management, and SSO stack too. Unbundling costs more than the bundle. Owned code lets you keep Gusto for payroll, your own MDM for devices, and your own onboarding flow — pick the best tool per job.

**Workday is a six-figure enterprise contract.** It also takes six months to implement. If you are 50 to 500 employees and you do not want to be Workday's customer for the next decade, generating your own onboarding stack and bending it to your process beats waiting on a Workday partner's Gantt chart.

## Who this is for

- **HR leads at 30 to 500 person companies** formalizing onboarding past the spreadsheet stage without committing to a per-seat platform forever.
- **Multi-location retail or hospitality operators** with high turnover where the per-seat math gets ugly fast and onboarding has to be repeatable across sites.
- **Remote-first companies** that need equipment shipping, account provisioning, and document collection automated because there is no office to walk the new hire around.
- **People-ops consultants** building bespoke onboarding stacks for client companies who want to own their HR data rather than rent it.

## Example entities generated

A typical AI HR onboarding platform generation produces entities like:

- `Employee` / `Role` / `Department`
- `OnboardingTemplate` / `ChecklistItem` / `TaskAssignment`
- `Document` / `SignatureRequest` / `SignedDocument`
- `EquipmentRequest` / `AccessRequest`
- `ManagerAssignment` / `OrgChartNode`
- `Location` / `WorkSchedule`
- `OnboardingMilestone` / `CompletionRecord`

The exact shape depends on your prompt. A 50-person SaaS startup generates different entities than a 300-location coffee chain.

### Real example: Remote-first software company, 80 employees

Imagine you submit this spec:

> "We are a remote-first software company with 80 employees across four time zones. New hires get role-based onboarding: engineers, designers, sales, and ops each have different checklists. Every new hire signs an offer letter, NDA, and I-9 before day one. Engineering hires get a laptop shipped, GitHub access, AWS read-only, and Slack invites provisioned automatically. Sales hires get a CRM seat and a Zoom Phone number. Managers see their direct reports' onboarding progress and get nudged about overdue tasks. We have day-1, week-1, and month-1 milestones tied to performance check-ins."

StackAlchemist generates:

- `Employee` entity with first_name, last_name, work_email, role_id, manager_id, start_date, location, employment_type (full-time, contractor)
- `Role` entity with title, department, onboarding_template_id, equipment_profile_id
- `OnboardingTemplate` entity with role_id, milestone_buckets (day_1, week_1, month_1), and ordered checklist_items
- `ChecklistItem` entity with template_id, title, description, assignee_type (employee, manager, IT, HR), due_offset_days
- `TaskAssignment` entity with employee_id, checklist_item_id, status (pending, in_progress, complete, blocked), completed_at
- `Document` entity with employee_id, document_type (I-9, W-4, offer_letter, NDA), storage_url, uploaded_at
- `SignatureRequest` entity with document_id, signer_email, status (sent, viewed, signed), signed_at, audit_trail
- `EquipmentRequest` entity with employee_id, equipment_profile (engineer_laptop_v3, sales_kit_v2), shipping_address, status, tracking_number
- `AccessRequest` entity with employee_id, system (github, aws, slack, crm), permission_level, granted_at, granted_by
- `ManagerAssignment` entity with employee_id, manager_employee_id, effective_date — used to build the org chart and route nudges
- API endpoints: `POST /employees`, `GET /employees/:id/onboarding-status`, `POST /onboarding/templates/:id/assign`, `PUT /tasks/:id/complete`, `POST /documents/upload`, `POST /signature-requests`, plus manager and HR-admin dashboards

All wired into a Next.js employee portal and manager dashboard with a .NET backend handling document storage, signature webhook callbacks, and milestone progression. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire your e-signature provider into the SignatureRequest scaffold.** The generated repo includes the signature-request entity, webhook endpoints, and audit-trail tables, but you drop in your own DocuSign or HelloSign API key. Set `DOCUSIGN_INTEGRATION_KEY` and `DOCUSIGN_USER_ID` in your `.env.local`, run the migrations, and send a test offer letter through the flow. You now have a working e-signature pipeline without paying a per-document HR SaaS markup on top of DocuSign's own pricing.

2. **Connect your equipment provisioning to a real workflow.** Maybe you ship from a Hofy or Firstbase account, or maybe IT pulls from a closet in the office. The generated `EquipmentRequest` entity is the seam. Add a webhook to your shipping provider, or wire it to a Slack channel where IT picks up the ticket and updates `tracking_number` when the laptop ships. Same pattern for access requests — pipe `AccessRequest` creation to your Okta SCIM endpoint, or just to a Slack channel for IT to action. The code is yours, so the integration looks like your actual process, not a vendor's idea of it.

## What is not included

StackAlchemist is not BambooHR. We do not run payroll, do not file your taxes, do not store benefits enrollments, and do not handle compliance reporting (EEO-1, ACA). We generate you the code for the onboarding side. You deploy it and you integrate it with whatever payroll and benefits stack you already use (Gusto, Justworks, ADP, Rippling-for-payroll-only — your call).

We do not include a built-in e-signature provider, document OCR, or background-check integration. We scaffold the entities, endpoints, and webhook handlers so you can wire in DocuSign, Checkr, or whoever you prefer. You stay in control of vendor choice, which means you also stay in control of cost.

## Pricing

One-time, per generation:

- **Simple-mode HR onboarding** — $299. Single-tenant, role-based checklists, document upload, manager dashboard, basic org chart.
- **Blueprint-tier** — $599. Adds equipment/access request workflow, e-signature scaffolding, multi-location support, milestone-driven check-ins.
- **Boilerplate-tier** — $999. Multi-company tenant model, custom reporting, deeper integrations (SCIM stubs, SSO scaffolding), advanced approval routing.

No monthly fee. No per-seat pricing as you hire. You own what you generate.

## Get started

Describe your onboarding process in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
