# Generate a full AI Learning Management System from a prompt

Teach, train, certify. StackAlchemist generates a production-shaped LMS as a .NET 10 + Next.js 15 codebase with courses, lessons, quizzes, and enrollments already wired. Compile-verified. Owned. Deployed wherever you want.

## What you get

A production-shaped AI LMS with:

- **Course catalog** with categories, tracks, and pricing tiers
- **Lesson structure** with modules, chapters, and rich content (video embeds, markdown, code samples)
- **Quizzes and assessments** with multiple-choice, short-answer, and auto-graded types
- **Student enrollments** tied to Stripe subscriptions or one-time purchases
- **Progress tracking** per student, per course, with completion certificates
- **Instructor dashboard** to manage courses, see enrollment data, respond to questions
- **Admin dashboard** for platform-wide analytics, payouts, and moderation
- **Email notifications** for enrollment, reminders, and certificates
- **Authentication** via Supabase — student, instructor, admin roles
- **Payments** via Stripe — one-time courses, monthly memberships, lifetime access
- **CI/CD** — GitHub Actions with compile verification
- **Docker-compose** for local development — one command to run the full stack

All generated in about 12 minutes. Compile-verified. Owned.

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
- API endpoints: `GET /courses/:id`, `POST /enrollments`, `POST /lessons/:id/progress`, `POST /quizzes/:id/submit`, `GET /certificates/:id/verify`, plus instructor and admin panels for content and analytics

The generated codebase wires Stripe one-time payments to enrollments, sends email on enrollment and unit completion, and generates a signed certificate link the student can download or share. The instructor dashboard shows live progress across all students. The admin panel has forms to add lessons, edit quiz questions, and manage pricing.

## After you own the code: two next steps

Once you have the repo:

1. **Connect your video host and configure email.** The generated codebase has placeholder fields for video URLs and email send integration. You create a Mux account (or Vimeo, or Bunny), upload your lessons, paste the CDN URLs into your admin panel, then wire your email provider (Resend, SendGrid, AWS SES — your choice) into the `EmailService` class. The .NET backend has the hooks pre-built; you just inject your credentials. One day of work, then students get real video and enrollment emails start flowing.

2. **Design your certificate template and add instructor-specific quizzes.** The generated certificate is a simple HTML template — you customize the colors, add your logo, and optionally add the student's name. For your second course, you might want different quiz types or a different quiz-per-lesson rhythm. You add new Question entity types (essay, code-submission, peer-review) by extending the quiz engine. The codebase is not locked to the first course; it scales to your whole catalog.

## What is not included

StackAlchemist does not host your videos. You plug in a video CDN (Mux, Vimeo, Bunny, S3 with CloudFront — whatever you prefer). We do not provide live streaming out of the box. We do not provide a mobile app — the generated Next.js site is mobile-responsive, but if you need native iOS/Android, that is a separate build.

We do not provide SCORM compliance, xAPI tracking, or formal LMS interoperability out of the box. If you need to export student data to a corporate LMS or run cohort synchronization with an external system, that is custom integration work. We give you the structure; you plug in the standards.

For course creators who want everything managed, hosted LMS platforms are simpler. For operators who want to own their stack, we are the faster path.

## Pricing

One-time, per generation:

- **Simple-mode LMS** — $299. Single institution, courses, lessons, quizzes, Stripe one-time payments.
- **Blueprint-tier** — $599. Adds subscriptions, memberships, cohort-based courses, deeper analytics.
- **Boilerplate-tier** — $999. Multi-institution, white-label, advanced certification, API for partner integrations.

No platform fee. No per-student charge. No revenue share. Generate once, deploy, operate.

## Get started

Describe your learning platform in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
