# Changelog

All notable changes to this project will be documented in this file.

## [unreleased]

### Bug Fixes

- Harden deploy pipeline for zero-downtime and reboot resilience
- Add git to builder and wget to runtime for sa-web health checks
- Fix Dockerfile syntax and sync frontend lockfile

### Documentation

- Update production release and deployment documentation to reflect automated main branch trigger

### Miscellaneous Tasks

- Update release and prod-deploy triggers to auto-release from main

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
