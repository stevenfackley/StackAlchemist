# Generate a full AI Healthcare Patient Portal from a prompt

You describe the kind of patient portal your clinic or telehealth practice needs. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI healthcare patient portal with:

- **Patient records** — `Patient`, `Demographics` and `InsurancePolicy` entities with MRN, contact details and insurance fields
- **Providers** — `Provider`, `Specialty` and `Location` entities with specialties, locations and an accepting-new-patients flag
- **Scheduling data** — `Appointment`, `Availability` and `AppointmentSlot` entities with status and duration
- **Messaging data** — `MessageThread` and `Message` entities linking patients and providers
- **Prescriptions** — `Prescription`, `Pharmacy` and `RefillRequest` entities
- **Documents** — `Document` and `DocumentCategory` entities with category, uploader and file URL for intake forms, insurance cards and lab results
- **Audit tables** — `AuditEvent` and `AccessLog` entities for recording record access
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** authentication and authorization — the generated repo has none. The Supabase client is preinstalled, but sign-in, roles and the rule that patients see only their own records are yours to write. So are audit logging on every record access, encryption of messages and documents, file storage for uploads, appointment reminders, and your CI pipeline. Build those before a single real patient record goes in.

Generation takes about 12 minutes from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of buying a portal product

**EHR vendor portals are bolted on, not designed in.** Epic, Cerner, Athenahealth all ship "patient portals" as a checkbox feature on top of their EHR. The UX shows it. Patients hate them. A generated portal is a real product, designed for the patient-facing experience first.

**Off-the-shelf telehealth platforms own your patients.** SimplePractice, Doxy.me, TheraNest — they hold your patient relationships, your scheduling data, your messaging history. If they change terms or raise prices, your practice's operations are at risk. Owned code means owned patients.

**Generic portals can't encode your specialty's workflow.** A pediatric clinic, an OB/GYN practice, a behavioral-health group, and a primary-care office all have meaningfully different intake forms, scheduling rules, and communication patterns. A generated portal is tuned for your specialty from the prompt up.

## Who this is for

- **Independent clinics** opening or replacing their patient-facing software stack who want owned infrastructure.
- **Telehealth startups** building patient-facing products that need a patient-data model to build on without committing to an enterprise EHR vendor.
- **Practice management groups** delivering branded portals to member clinics that need to remain on-brand and self-hosted.
- **Healthcare-vertical SaaS founders** building specialty-specific products who need the patient-portal foundation as a starting point.

## Example entities generated

A typical AI Healthcare Patient Portal generation produces entities like:

- `Patient` / `Demographics` / `InsurancePolicy`
- `Provider` / `Specialty` / `Location`
- `Appointment` / `Availability` / `AppointmentSlot`
- `Message` / `MessageThread`
- `Prescription` / `Pharmacy` / `RefillRequest`
- `Document` / `DocumentCategory`
- `AuditEvent` / `AccessLog`

The exact shape depends on your prompt. A pediatric clinic generates different entities than a telehealth-only behavioral health practice.

### Real example: Small primary-care clinic

Imagine you submit this spec:

> "We run a primary-care clinic with four providers. Patients book appointments online, see their providers' availability, and reschedule themselves up to 24 hours before. Patients can message providers asynchronously and upload intake forms and insurance cards. Providers see their patient list, message inbox, and the day's schedule. Admins manage provider availability and run reports on no-show rates and visit volume. Every record access is audit-logged for compliance review."

StackAlchemist generates:

- `Patient` entity with name, DOB, MRN (medical record number), contact info, primary_provider_id
- `InsurancePolicy` entity with patient_id, payer, policy_number, group_number, scanned_card_document_id
- `Provider` entity with name, specialty_id, accepting_new_patients, default_appointment_duration_minutes
- `Availability` entity with provider_id, day_of_week, start_time, end_time, recurring flag
- `Appointment` entity with patient_id, provider_id, scheduled_at, duration, status (scheduled, confirmed, in-progress, completed, no-show, cancelled)
- `MessageThread` + `Message` entities — threads link patient + provider; encrypting message bodies at rest is yours to add
- `Document` entity with patient_id, category (intake, insurance, lab-result), uploaded_by, file_url, uploaded_at
- `AuditEvent` entity with actor_user_id, action, target_record_type, target_record_id, occurred_at, ip_address
- CRUD endpoints for every entity (`/api/v1/patients`, `/api/v1/appointments`, `/api/v1/messages`, …), open until you add authentication. Endpoints like `PATCH /appointments/:id/reschedule` or a no-show report can be declared in Advanced Mode; the 24-hour rescheduling rule and the reporting logic behind them are yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the portal's data model and CRUD layer, compile-verified. It is not access-controlled. The patient portal, the provider and admin views, authorization checks on every patient record read, the middleware that writes an `AuditEvent` on each access, and encryption are code you add before go-live. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Build the access controls, then have your compliance counsel review them.** The generated portal ships none of the table-stakes controls for healthcare data: no sign-in, no role checks, no access logging, no application-level encryption. Row-level security is enabled on every table, with no policies written. Add sign-in (the Supabase client is preinstalled, or use your own identity provider), enforce that patients see only their own records and providers only their assigned patients, write the middleware that records every access to `AuditEvent`, encrypt messages and documents at rest, and deploy HTTPS-only. Then your counsel reviews your BAAs with cloud providers, your access policies, and your incident response plan. HIPAA compliance is an operational program, not a code feature, and the controls that support it are yours to build.

2. **Wire your e-prescribing and labs-results integrations.** The generated portal includes `Prescription` and lab `Document` entities but does not include direct integrations with Surescripts (for e-prescribing) or LabCorp / Quest (for results delivery). Those integrations are specialty-specific contracts and depend on your patient population. Add them once your clinic has actual prescribing volume — the generated entities are the data layer they will plug into.

## What is not included

StackAlchemist is not Epic. We don't host your portal, don't provide HIPAA compliance certification, and don't ship a full EHR. We generate you the code. You deploy and operate it.

We don't include clinical decision support, ICD-10 coding helpers, or insurance-claim submission out of the box — those are deep clinical and billing systems with their own integration surface. The portal you get is the patient-facing data model and CRUD layer, plus the `AuditEvent` table your audit logging will write to. Clinical and billing systems are downstream products you integrate once the portal is running.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No per-patient tax. You own what you generate.

## Get started

Describe your patient portal in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
