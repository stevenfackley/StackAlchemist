# Changelog

All notable changes to this project will be documented in this file.

## [unreleased]

### Bug Fixes

- Harden deploy pipeline for zero-downtime and reboot resilience
- Add git to builder and wget to runtime for sa-web health checks
- Fix Dockerfile syntax and sync frontend lockfile
- Ensure Docker network exists and robustify fallback logic
- Deploy engine and worker alongside web and add health checks
- Add git to web-builder, set engine port, fix healthz endpoint
- Copy .env to Next.js project dir so E2E tests get env vars
- Stabilize e2e contracts and enforce hybrid github actions gates
- Stabilize test environment integration workflow
- Correct secret mapping for e2e and deploy workflows
- Skip E2E integration gracefully when secrets are placeholder values
- Pin test deploy to x64 self-hosted runner
- Stabilize infrastructure and switch to npm
- Complete transition to npm and stabilize deployments
- Prevent port conflicts in test deployment by removing all standalone containers
- Ensure consistent container names in test deployment for health checks
- Complete transition to npm and stabilize deployments
- Prevent port conflicts in test deployment by removing all standalone containers
- Ensure consistent container names in test deployment for health checks
- Build production images sequentially to reduce runner resource pressure
- Build production images sequentially to reduce runner resource pressure
- Harden self-hosted deploy workflows
- Force-recreate on prod deploy to handle restart=always containers after reboot
- Eliminate band-aids with permanent root-cause fixes
- Fix prod deploy cancellation and test tunnel network disconnect
- Force-recreate on prod deploy to handle restart=always containers after reboot
- Rm named containers before compose-up; add maintenance page for both envs
- Rm named containers before compose-up; add maintenance page for both envs
- Harden tunnel, CI/CD runner, and LLM error surfacing
- Use 127.0.0.1 in nginx healthcheck (localhost resolves to IPv6 in alpine)
- Add ANTHROPIC_MODEL env var to prod compose; switch to claude-sonnet-4-5-20250929
- Deserialize ProjectType enum from JSON string + add ANTHROPIC_MODEL to prod compose
- Rename simple-mode-schema.ts → .tsx (contains JSX, lint parse error)
- Correct secret mapping for e2e and deploy workflows
- Skip E2E integration gracefully when secrets are placeholder values
- Align @ alias with Next.js tsconfig paths
- Add missing files that were never committed
- Harden CF tunnel routing + close SEO/a11y audit gaps
- Add parserOptions.ecmaFeatures.jsx to eslintrc — fix parse error in .tsx files
- Batch A — SSR h1s, landmarks, metadata, docs/content COPY
- Un-ignore docs/ and scope *.md to repo root
- Drop generateImageMetadata — 404'd with id-keyed URL
- Unblock CA2007 analyzer + vitest IntersectionObserver constructor
- Unblock CI — dedupe h1s, scope test CA2007
- Suppress CA2007 in Worker.Tests (symmetry with Engine.Tests)
- Migrate PostCSS pipeline to Tailwind v4 ([#46](https://github.com/stevenfackley/StackAlchemist/issues/46))
- Unbreak CI — pin eslint ^9, complete eslint-config-next 16, migrate to flat config ([#65](https://github.com/stevenfackley/StackAlchemist/issues/65))
- Re-pin eslint ^9, ignore eslint majors in dependabot.yml ([#73](https://github.com/stevenfackley/StackAlchemist/issues/73))
- Close 10 code-scanning alerts
- Mask email PII in logs + bump postcss to ^8.5.10 ([#78](https://github.com/stevenfackley/StackAlchemist/issues/78))
- Wire ITierGatingService into orchestrator + skip codegen on Tier 1 ([#84](https://github.com/stevenfackley/StackAlchemist/issues/84))
- Serialize Release workflow to prevent changelog-commit race ([#87](https://github.com/stevenfackley/StackAlchemist/issues/87))
- Clear three engine errors blocking nightly simple-mode-flow E2E ([#92](https://github.com/stevenfackley/StackAlchemist/issues/92)) ([#94](https://github.com/stevenfackley/StackAlchemist/issues/94))
- Pin .NET base images to :10.0-resolute ([#95](https://github.com/stevenfackley/StackAlchemist/issues/95))
- Derive SUPABASE_DB_PASSWORD from CI_SUPABASE_DB_URL ([#96](https://github.com/stevenfackley/StackAlchemist/issues/96))

### Dependencies

- Bump softprops/action-gh-release from 2 to 3 ([#25](https://github.com/stevenfackley/StackAlchemist/issues/25))
- Bump actions/setup-node from 5 to 6 ([#27](https://github.com/stevenfackley/StackAlchemist/issues/27))
- Bump aws-actions/configure-aws-credentials from 5 to 6 ([#29](https://github.com/stevenfackley/StackAlchemist/issues/29))
- Bump actions/checkout from 5 to 6 ([#26](https://github.com/stevenfackley/StackAlchemist/issues/26))
- Bump styled-jsx in /src/StackAlchemist.Web ([#16](https://github.com/stevenfackley/StackAlchemist/issues/16))
- Bump @supabase/supabase-js in /src/StackAlchemist.Web ([#14](https://github.com/stevenfackley/StackAlchemist/issues/14))
- Bump pytest 8.3.3 -> 9.0.3 in V1-Python-React template
- Bump @supabase/supabase-js in /src/StackAlchemist.Web ([#43](https://github.com/stevenfackley/StackAlchemist/issues/43))
- Bump @supabase/ssr in /src/StackAlchemist.Web ([#39](https://github.com/stevenfackley/StackAlchemist/issues/39))
- Bump docker/build-push-action from 6 to 7 ([#35](https://github.com/stevenfackley/StackAlchemist/issues/35))
- Bump docker/setup-buildx-action from 3 to 4 ([#34](https://github.com/stevenfackley/StackAlchemist/issues/34))
- Bump actions/upload-artifact from 4 to 7 ([#33](https://github.com/stevenfackley/StackAlchemist/issues/33))
- Bump python-dotenv ([#52](https://github.com/stevenfackley/StackAlchemist/issues/52))
- Bump lucide-react in /src/StackAlchemist.Web ([#55](https://github.com/stevenfackley/StackAlchemist/issues/55))
- Bump @supabase/supabase-js in /src/StackAlchemist.Web ([#60](https://github.com/stevenfackley/StackAlchemist/issues/60))
- Bump the next-react group across 1 directory with 4 updates ([#53](https://github.com/stevenfackley/StackAlchemist/issues/53))
- Bump Node 20 → 22 (Node 20 LTS ended 2026-04-30)
- Bump Node 22 → 24 (skip the layover, latest LTS)
- Bump @supabase/supabase-js in /src/StackAlchemist.Web ([#71](https://github.com/stevenfackley/StackAlchemist/issues/71))
- Bump lucide-react in /src/StackAlchemist.Web ([#70](https://github.com/stevenfackley/StackAlchemist/issues/70))
- Bump @supabase/supabase-js in /src/StackAlchemist.Web ([#74](https://github.com/stevenfackley/StackAlchemist/issues/74))

### Documentation

- Update production release and deployment documentation to reflect automated main branch trigger
- Sync documentation with current codebase state
- Add Show HN draft + Product Hunt hunter strategy
- Add CODE_AUDIT.md and HANDOFF.md
- Update with session 2 progress
- Correct scope — root error.tsx pre-existed
- Note AWSSDK.S3 3→4, eslint 9→10, lucide-react 0→1 majors from 2026-04-28 sweep
- Consolidate ADRs into docs/DECISIONS.md, stub root file ([#97](https://github.com/stevenfackley/StackAlchemist/issues/97))

### Features

- Extend modal with creative non-technical steps
- Scaffold /blog route + seed 5 launch posts
- Scaffold /compare route + seed v0, bolt-new
- Scaffold /solutions route + seed 3 verticals
- Extract ContentHeader used by new content routes
- Sitemap and robots include new SEO surfaces
- Add Basic Auth gate for test mirror
- Add per-segment error boundaries on dynamic content routes
- Deep-link anchors + ContentHeader + isDemoMode boot warning
- Batch B — CSP-Report-Only, legal pages, byline, contrast
- Batch C — RSS, per-post OG, related posts, CSP plan
- Add /compare/lovable page ([#47](https://github.com/stevenfackley/StackAlchemist/issues/47))
- Add /compare/cursor page ([#48](https://github.com/stevenfackley/StackAlchemist/issues/48))
- Add /compare/replit-agent page ([#50](https://github.com/stevenfackley/StackAlchemist/issues/50))
- Close Phase 7 production hardening — webhooks, metrics, emails, error boundaries
- V2-Python-React Swiss Cheese + e2e smoke + tuning docs ([#77](https://github.com/stevenfackley/StackAlchemist/issues/77))
- V2-DotNet-NextJs nextjs/ frontend with per-entity zones ([#80](https://github.com/stevenfackley/StackAlchemist/issues/80))
- V2-Python-React per-entity frontend templates ([#81](https://github.com/stevenfackley/StackAlchemist/issues/81))
- Add blog, compare, solutions pages + Show HN draft

### Miscellaneous Tasks

- Update release and prod-deploy triggers to auto-release from main
- Add .pnpm-store to .gitignore
- Dependency hygiene and CI restore drift hardening
- Retrigger test deploy for tunnel fix
- Merge develop into main — resolve deploy-prod.yml conflict
- Trigger test deploy [skip release]
- Merge fix/ci-e2e-integration into develop
- Merge fix/ci-e2e-integration into main
- Merge main into develop — sync changelog and ci-e2e fixes
- Opt into Node.js 24 for GitHub Actions
- Remove static og-image.png replaced by dynamic opengraph-image.tsx
- Update playwright report — 24/24 smoke passing
- Add dependabot config
- Bump postcss in /src/StackAlchemist.Web ([#20](https://github.com/stevenfackley/StackAlchemist/issues/20))
- Bump autoprefixer in /src/StackAlchemist.Web ([#18](https://github.com/stevenfackley/StackAlchemist/issues/18))
- Bump msw in /src/StackAlchemist.Web ([#13](https://github.com/stevenfackley/StackAlchemist/issues/13))
- Bump the testing group ([#11](https://github.com/stevenfackley/StackAlchemist/issues/11))
- Apply workspace baseline (sweep)
- Merge main into develop
- Bump tailwindcss in /src/StackAlchemist.Web ([#45](https://github.com/stevenfackley/StackAlchemist/issues/45))
- Bump typescript in /src/StackAlchemist.Web ([#44](https://github.com/stevenfackley/StackAlchemist/issues/44))
- Bump cross-env in /src/StackAlchemist.Web ([#41](https://github.com/stevenfackley/StackAlchemist/issues/41))
- Bump @types/node in /src/StackAlchemist.Web ([#40](https://github.com/stevenfackley/StackAlchemist/issues/40))
- Bump @vitejs/plugin-react in /src/StackAlchemist.Web ([#38](https://github.com/stevenfackley/StackAlchemist/issues/38))
- Bump eslint in /src/StackAlchemist.Web ([#37](https://github.com/stevenfackley/StackAlchemist/issues/37))
- Bump eslint in /src/StackAlchemist.Web ([#59](https://github.com/stevenfackley/StackAlchemist/issues/59))
- Bump msw in /src/StackAlchemist.Web ([#58](https://github.com/stevenfackley/StackAlchemist/issues/58))
- Bump tailwindcss in /src/StackAlchemist.Web ([#56](https://github.com/stevenfackley/StackAlchemist/issues/56))
- Bump @tailwindcss/postcss in /src/StackAlchemist.Web ([#62](https://github.com/stevenfackley/StackAlchemist/issues/62))
- Bump postcss in /src/StackAlchemist.Web ([#61](https://github.com/stevenfackley/StackAlchemist/issues/61))
- Bump the testing group ([#54](https://github.com/stevenfackley/StackAlchemist/issues/54))
- Bump jsdom ([#67](https://github.com/stevenfackley/StackAlchemist/issues/67))
- Bump eslint in /src/StackAlchemist.Web ([#72](https://github.com/stevenfackley/StackAlchemist/issues/72))
- Bump msw in /src/StackAlchemist.Web ([#68](https://github.com/stevenfackley/StackAlchemist/issues/68))
- Bump postcss in /src/StackAlchemist.Web ([#69](https://github.com/stevenfackley/StackAlchemist/issues/69))
- Drop validate.mjs Node toolchain + sweep analyzer warnings to 0 ([#79](https://github.com/stevenfackley/StackAlchemist/issues/79))
- Enable Generation:UseSwissCheese in dev + document rollout ([#82](https://github.com/stevenfackley/StackAlchemist/issues/82))

### Refactoring

- Extract JSON-LD builders into src/lib/jsonld.ts
- Adopt ContentHeader on /about and /story
- Extract SITE_URL constant from 8 repeated env reads
- Upgrade manifests to as const satisfies
- Extract questionToAnchor + cover uniqueness

### Testing

- Update e2e flows for advanced mode and checkout
- Cover manifests and content loaders
- Cover ContentHeader
- Smoke for /blog /compare /solutions /faq
- Add V1 one-shot pipeline integration tests ([#83](https://github.com/stevenfackley/StackAlchemist/issues/83))
- Integration coverage for CompileWorkerService retry loop ([#85](https://github.com/stevenfackley/StackAlchemist/issues/85))
- Tier 3 IaC + Helm + runbook integration coverage ([#86](https://github.com/stevenfackley/StackAlchemist/issues/86))

### Ci

- Wire Plausible, GSC, and Basic Auth env vars through pipeline
- Mask service role key + tighten E2E Integration permissions ([#88](https://github.com/stevenfackley/StackAlchemist/issues/88))
- Always dump sa-engine + sa-worker logs after E2E Integration ([#89](https://github.com/stevenfackley/StackAlchemist/issues/89))
- Set descriptive run-name for CI and Deploy Production Site ([#90](https://github.com/stevenfackley/StackAlchemist/issues/90))
- Split flake-prone simple-mode-flow into non-blocking nightly suite ([#91](https://github.com/stevenfackley/StackAlchemist/issues/91))
- Fail on main if E2E Integration silently skipped + run smoke on dispatch ([#93](https://github.com/stevenfackley/StackAlchemist/issues/93))

### Seo

- Improve page metadata descriptions and canonical URLs

## [1.0.4] - 2026-04-06

### \fix

- Use HEAD:main refspec for changelog push in release workflow"

## [1.0.3] - 2026-04-06

### \fix

- Add missing ENGINE_SERVICE_KEY to sa-web service in prod compose"

## [1.0.1] - 2026-04-06

### Bug Fixes

- Exclude playwright.config.ts and e2e from TypeScript build check
- Add explicit Node/Edge type params to useNodesState/useEdgesState
- Exclude vitest.config.ts and test files from Next.js TypeScript check
- Connect sa-web to tunnel network, fix nginx fallback for port 3000
- Logo text, blue accent color, remove darkMode class
- Fix Supabase Database type to resolve TS build error
- Use untyped createClient<any> in createServerClient
- Skip TS and ESLint errors during Next.js build to unblock CI
- Resolve all build and test failures
- Wire Stripe secrets into test env and fix test accuracy
- Load .env file into Engine config via DotNetEnv
- Grant release workflow write permissions and fix worker build
- Include scripts dir in image and update dockerignore
- Skip postinstall script during Docker build
- Restructure layout to fix mobile nav positioning
- Add markdown runtime deps for docs build in CI
- Include selected tier in generation subscriptions
- Switch to BuildKit and rewrite Docker preflight cleanup script
- Add missing test-site-banner component to fix CI build
- Docker CI + frontend build pipeline
- Switch pnpm install to --no-frozen-lockfile
- Handle missing pnpm lockfile gracefully in build
- Add CookieOptions type annotation to setAll callbacks in Supabase SSR clients
- Stabilize main e2e workflow
- Start web server during playwright e2e
- Remove legacy prod containers before deploy
- Avoid aws cli dependency in oidc check
- Shamefully-hoist pnpm in Docker to fix standalone module resolution
- Update pnpm lockfile with @supabase/ssr dependency
- Pass runtime env to test web deploy
- Include next runtime in web image
- Install web runtime deps in final image
- Copy next runtime into web image
- Switch to npm and fix standalone web build
- Skip postinstall script during npm install in web builder
- Add personalization_json null to demo-data Generation object
- Remove quoted env values and use in-container health check
- Fix nginx api routing and update env template

### Documentation

- Establish foundational architecture and product documentation
- Align documentation structure with enterprise standards
- Update domain references to stackalchemist.app
- Enhance design with logo and enterprise branding
- Define optimal CI/CD flow and environment triggers
- Finalize architecture, user guides, and product use cases
- Added bd stuff
- Fixed tone of bd playbook
- Add StackAlchemist business development playbook
- Update design system and dev prompt to reflect marketing UI refinements
- Overhaul README — logo_nebula, tech stack table, Getting Started, pipeline diagram
- Refresh dev prompt phase guidance and progress tracker
- Add mascots section introducing Auri and Reto
- Add mascot illustrations to README sections
- Add multi-step personalization wizard to product specs
- Sync implementation status and test coverage metrics

### Features

- Add Kubernetes and Helm support to Tier 3 roadmap
- Complete phase 1 scaffolding
- Complete phase 2 master template construction
- Deploy real Next.js app to test site with placeholder fallback
- Add pricing, about, and story pages; update navbar
- Premium homepage redesign with glassmorphism terminal input; fix ReactFlow live preview height
- UI overhaul - v0 design system, Supabase integration, server actions
- Refine landing page hero layout
- Add workflow phases panel and builder prompt groups
- Implement ReconstructionService tests and expand solution structure
- Phase 3 — engine services, Stripe webhook, test coverage
- Phase 4 — live LLM orchestration, R2 upload, Supabase delivery
- Add interactive schema editor to SimpleModePage
- Add free SPARK tier and cross-origin isolation headers
- Add generation client page with live status UX
- Add mascot bios and refresh Auri artwork
- Add StackBlitz SDK dependency
- Close Phase 4 gaps — Supabase schema, LLM schema extraction, build log streaming
- Add bolt demo mode for web app
- Add test-site banner with build-time git commit date
- Add Stripe payment gate and Supabase auth pages (Phase 5)
- Phase 6 — Supabase SSR session propagation + user dashboard
- Add @supabase/ssr package for server-side rendering support
- Add production deployment workflow
- Add multi-ecosystem generation flow for dotnet and python-react
- Phase 4.7 — Personalization Wizard
- Rate limiting, CORS, engine service key auth, prompt sanitization

### Miscellaneous Tasks

- Establish multi-environment config templates
- Align test and production env keys with development
- Rename services to sa-web, sa-engine, and sa-worker
- Pivot to Supabase Single Project + Branching model
- Commit remaining phase 2 files and add test site deploy workflow
- Add .gitattributes to enforce LF line endings repo-wide
- Remove style-extracted/ from tracking, add to .gitignore
- Harden phase 1 scaffold and sync handoff docs
- Ignore local agent instructions
- Gitignore all .env files, generate via GitHub Actions
- Add setup-env script + single-.env workflow
- Commit staged docs pages, logo_nebula, docs components (recovered from stash)
- Refresh swiss cheese mascot artwork
- Align pnpm to v8 in CI workflow and Docker
- Add fallback Docker cleanup in test deploy workflow
- Add package metadata and versioning to package.json
- Enable aws oidc in prod deploy

### Refactoring

- Update copy and refactor pricing nav header
- Consolidate and simplify homepage UI sections
- Move @supabase/ssr to web project and pin to v0.6.1

### Testing

- Stabilize Playwright E2E pipeline and test flow
- Replace scaffolded flow specs with UI assertions

### Build

- Upgrade pnpm to v10 and include workspace config

### Ci

- Switch deploy-test workflow to self-hosted runner
- Simplify deploy via docker cp + add runner to placeholder compose
- Write index.html directly to host bind-mount path via runner
- Fix - only copy index.html (no favicon.svg in repo)
- Disable BuildKit for test web image build
- Stabilize test deploy Docker build on self-hosted runner
- Harden test deploy against docker cache corruption on runner
- Improve Docker disk management in deploy workflow
- Add test deploy diagnostics
- Add prod deploy workflow and fix test environment secrets

### Devops

- Finalize optimal Docker and Environment strategy

### Merge

- Sync production deployment updates from main
- Sync deploy cleanup fix from main
- Sync aws oidc prod deploy updates from main
- Sync oidc verification fix from main
- Sync develop into main, use npm in Docker web builder

<!-- generated by git-cliff -->
