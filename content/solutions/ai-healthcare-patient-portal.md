# Generate a full AI Healthcare Patient Portal from a prompt

You describe the kind of patient portal your clinic or telehealth practice needs. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI healthcare patient portal with:

- **Patient records** with demographics, insurance information, and contact history
- **Appointment scheduling** with provider availability, rescheduling, and reminders
- **Provider directory** with specialties, locations, and online-booking enabled flags
- **Secure messaging** between patients and providers — server-side encrypted at rest
- **Prescription management** with active prescriptions, refill requests, and pharmacy linkage scaffolding
- **Document upload** for intake forms, insurance cards, and lab results with audit logging
- **Audit trail** on every record access — required for any healthcare data workflow
- **Role-based access control** so patients see only themselves, providers see their assigned patients, admins see everything
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

## Why generate it instead of buying a portal product

**EHR vendor portals are bolted on, not designed in.** Epic, Cerner, Athenahealth all ship "patient portals" as a checkbox feature on top of their EHR. The UX shows it. Patients hate them. A generated portal is a real product, designed for the patient-facing experience first.

**Off-the-shelf telehealth platforms own your patients.** SimplePractice, Doxy.me, TheraNest — they hold your patient relationships, your scheduling data, your messaging history. If they change terms or raise prices, your practice's operations are at risk. Owned code means owned patients.

**Generic portals can't encode your specialty's workflow.** A pediatric clinic, an OB/GYN practice, a behavioral-health group, and a primary-care office all have meaningfully different intake forms, scheduling rules, and communication patterns. A generated portal is tuned for your specialty from the prompt up.

## Who this is for

- **Independent clinics** opening or replacing their patient-facing software stack who want owned infrastructure.
- **Telehealth startups** building patient-facing products that need real medical data handling without committing to an enterprise EHR vendor.
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
- `MessageThread` + `Message` entities — threads link patient + provider, messages are encrypted at rest
- `Document` entity with patient_id, category (intake, insurance, lab-result), uploaded_by, file_url, uploaded_at
- `AuditEvent` entity with actor_user_id, action, target_record_type, target_record_id, occurred_at, ip_address
- API endpoints: `POST /appointments`, `PATCH /appointments/:id/reschedule`, `POST /messages`, `GET /patients/:id` (provider auth required), plus admin reporting endpoints
- A Next.js patient portal, a provider clinical-side view, and an admin operations panel
- Audit logging middleware that fires on every record access and writes to `AuditEvent` with the actor's session info

All wired into a Next.js frontend and a .NET backend with strict authorization checks on every patient record read. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Have your compliance counsel review the audit log scope and data handling.** The generated portal includes audit logging, role-based access, encryption at rest, and HTTPS-only deployment posture — the table-stakes controls for healthcare data. But HIPAA compliance is an operational program, not a code feature. Your counsel needs to review your BAAs with cloud providers, your access policies, and your incident response plan. The code is HIPAA-consistent. The compliance program is yours to operate.

2. **Wire your e-prescribing and labs-results integrations.** The generated portal includes `Prescription` and lab `Document` entities but does not include direct integrations with Surescripts (for e-prescribing) or LabCorp / Quest (for results delivery). Those integrations are specialty-specific contracts and depend on your patient population. Add them once your clinic has actual prescribing volume — the generated entities are the data layer they will plug into.

## What is not included

StackAlchemist is not Epic. We don't host your portal, don't provide HIPAA compliance certification, and don't ship a full EHR. We generate you the code. You deploy and operate it.

We don't include clinical decision support, ICD-10 coding helpers, or insurance-claim submission out of the box — those are deep clinical and billing systems with their own integration surface. The portal you get is patient-facing operations plus the audit-log skeleton for compliance. Clinical and billing systems are downstream products you integrate once the portal is running.

## Pricing

One-time, per generation:

- **Simple-mode patient portal** — $299. Single practice, appointment booking, basic messaging, patient document upload.
- **Blueprint-tier** — $599. Multi-provider, availability rules, prescription request workflow, audit logging.
- **Boilerplate-tier** — $999. Multi-location, advanced reporting, custom intake forms per specialty, e-prescribing scaffolding, role-based admin views.

No monthly fee. No per-patient tax. You own what you generate.

## Get started

Describe your patient portal in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
