# Generate a full AI Learning Management System from a prompt

Teach, train, certify. StackAlchemist generates a production-shaped LMS as a .NET 10 + Next.js 16 codebase with courses, lessons, quizzes, and enrollments modeled, each with CRUD endpoints. Compile-verified. Owned. Deployed wherever you want.

## What you get

A production-shaped AI LMS with:

- **Course catalog** — `Course` entities with categories, tracks and prices
- **Lesson structure** — `Module` and `Lesson` entities with order, video URLs and markdown content
- **Quizzes and assessments** — `Quiz`, `Question` and `AnswerChoice` entities with question types (multiple-choice, short-answer) and passing scores
- **Enrollments and progress** — `Enrollment`, `Progress` and `Completion` entities per student, per course
- **Certificates** — `Certificate` and `CertificateTemplate` entities with verification tokens
- **Instructors and students** — `Instructor` and `Student` entities
- **Payment records** — `Subscription` and `Payment` entities ready for your payment integration
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — one command to run the full stack

**What you wire yourself:** Stripe for one-time courses, memberships or lifetime access, sign-in with student, instructor and admin roles (the Supabase client is preinstalled; the auth flows are yours to write), quiz grading, certificate generation, enrollment and reminder emails, the instructor and admin dashboards, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

All generated in about 12 minutes. Compile-verified on the Boilerplate and Infrastructure tiers. Owned.

## Why generate an LMS instead of using Teachable / Thinkific / Podia

**Platform fees are a silent tax.** Teachable takes 5% of every transaction. Thinkific has tier-based limits. Over a year of serious course sales, platform fees run into five or six figures for a medium-sized business. An owned LMS pays that math back in weeks.

**Customization hits a wall fast.** Every hosted LMS has an opinion about what your course detail page should look like, how quizzes should work, how your brand should render. When you outgrow those opinions, you are stuck.

**Your student data is not yours.** Platforms retain the data; they merely rent you access. If you need to migrate, export tools are second-class. Owning the code means owning the database means owning the relationship.

**Integrations are the hardest part.** Integrating with your CRM, email system, analytics, and community tools is typically hours of glue per integration. In an owned codebase, you write the integrations once and own them.

## Who this is for

- **Course creators** doing $50K+/year who are hitting the ceiling of hosted platforms.
- **Corporate training teams** that need an LMS customized for their training taxonomy, compliance workflow, and SSO.
- **Coding bootcamps** and educational programs with specialized assessment needs.
- **Trade associations** offering CE credits, certificate tracking, and member-exclusive content.

## Example entities generated

A typical LMS generation produces:

- `Course` / `Module` / `Lesson`
- `Quiz` / `Question` / `AnswerChoice`
- `Enrollment` / `Progress` / `Completion`
- `Certificate` / `CertificateTemplate`
- `Instructor` / `Student`
- `Subscription` / `Payment`
- `Review` / `Discussion`

The domain adapts to your prompt. A yoga-teacher certification platform has different needs than a SQL bootcamp.

### Real example: Freelance web design certification track

Imagine you spec this:

> "We teach a 12-week web design certification. There are three units: Design Foundations, UI Systems, and Production-Ready Design. Each unit has 4-5 lessons with video, slides, and reading material. After each unit, students take a quiz. Pass all three quizzes and they get a certificate. Students enroll via Stripe one-time payment ($299). We need an instructor dashboard to see student progress, an admin panel to manage courses and content, and email notifications when someone enrolls or completes a unit."

StackAlchemist generates:

- `Course` entity with title, description, price, and instructor_id
- `Unit` entity (mapped to Module) with course_id, order, and title
- `Lesson` entity with unit_id, title, video_url (where you link to Mux/Vimeo later), content_markdown, order, and duration_minutes
- `Quiz` entity with unit_id, passing_score_percent
- `Question` entity with quiz_id, type (multiple_choice, short_answer), text, and points
- `AnswerChoice` entity with question_id, text, is_correct
- `StudentAnswer` entity with question_id, student_id, selected_choice_id, text_response
- `Enrollment` entity with course_id, student_id, enrolled_at, status
- `LessonProgress` entity with enrollment_id, lesson_id, watched_at, watched_percent
- `QuizAttempt` entity with quiz_id, enrollment_id, score, passed, attempted_at
- `Certificate` entity with course_id, enrollment_id, issued_at, verification_token
- CRUD endpoints for every entity (`/api/v1/courses`, `/api/v1/lessons`, `/api/v1/enrollments`, …). Endpoints like `POST /quizzes/:id/submit` or `GET /certificates/:id/verify` can be declared in Advanced Mode; the grading and verification logic behind them is yours to write
- Next.js types and a typed API client for every entity, plus starter pages

The generated codebase is the course data model and its CRUD layer, compile-verified. The Stripe payment that creates an enrollment, the emails on enrollment and unit completion, the signed certificate link a student can download or share, the instructor progress dashboard and the admin content forms are code you build on it. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once you have the repo:

1. **Connect your video host, Stripe and email.** The generated `Lesson` entity has a `video_url` field. You create a Mux account (or Vimeo, or Bunny), upload your lessons, and store the CDN URLs on each lesson. Then add the integrations the repo does not include: a Stripe Checkout for the $299 enrollment with a webhook that creates the `Enrollment`, and an `EmailService` backed by your email provider (Resend, SendGrid, AWS SES — your choice) for enrollment and completion emails. Once that is in, students get real video and enrollment emails start flowing.

2. **Build the certificate and extend the quizzes.** The `Certificate` entity has a verification token; the certificate itself — an HTML template with your colors, your logo and the student's name — is yours to design. For your second course, you might want different quiz types or a different quiz-per-lesson rhythm. You add new question types (essay, code-submission, peer-review) to the `Question` entity and write the grading for each. The codebase is not locked to the first course; it scales to your whole catalog.

## What is not included

StackAlchemist does not host your videos. You plug in a video CDN (Mux, Vimeo, Bunny, S3 with CloudFront — whatever you prefer). We do not provide live streaming out of the box. We do not provide a mobile app — the generated frontend is a Next.js web app, and if you need native iOS/Android, that is a separate build.

We do not provide SCORM compliance, xAPI tracking, or formal LMS interoperability out of the box. If you need to export student data to a corporate LMS or run cohort synchronization with an external system, that is custom integration work. We give you the structure; you plug in the standards.

For course creators who want everything managed, hosted LMS platforms are simpler. For operators who want to own their stack, we are the faster path.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No platform fee. No per-student charge. No revenue share. Generate once, deploy, operate.

## Get started

Describe your learning platform in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
