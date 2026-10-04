# Self-Hosting StackAlchemist

StackAlchemist is source-available under the StackAlchemist Source Available License (v1.0). You can read the code, fork it for personal experimentation and run it for non-commercial, educational or evaluation purposes. Commercial use requires a purchased tier at [stackalchemist.app](https://stackalchemist.app).

This guide covers running the code in this repository on your own machine or server.

---

## License Summary

The full text is in the repository's `LICENSE` file. In short:

| You may | You may not |
|---------|-------------|
| View and study the source | Sell, rent, sublicense or redistribute the Software or modified versions for profit |
| Fork it on GitHub for personal experimentation | Use it to build a product or service that competes with StackAlchemist |
| Run it for non-commercial, educational or evaluation use | Host it as a public service or integrated platform |
| | Remove copyright, trademark or proprietary notices |

"Commercial Use" means using the Software for direct or indirect financial gain, for example running a SaaS platform, selling it, or using it to provide paid consulting. Using the source to build a production application without a purchased tier (Tier 1, 2 or 3) violates the license. To use StackAlchemist commercially, buy a tier at [stackalchemist.app](https://stackalchemist.app).

---

## What You Run

| Component | What it is | Port |
|-----------|-----------|------|
| `sa-web` | Next.js 16 frontend (`src/StackAlchemist.Web`) | 3000 |
| `sa-engine` | .NET 10 Web API (`src/StackAlchemist.Engine`), which also runs the Compile Guarantee worker in-process | 5000 locally (80 in the container) |
| Postgres | Any Postgres you provide, reached through `DATABASE_URL` | yours |
| Keycloak | A realm for sign-in, reached through `QAVREN_AUTH_URL` (optional, see demo mode below) | yours |

You do not need to start the `StackAlchemist.Worker` project. The compile worker runs inside the Engine process. The Worker project exists for scale-out and the hosted platform does not deploy it.

External services the full pipeline uses: Anthropic (generation), Cloudflare R2 (archive storage, S3-compatible), Stripe (checkout and refunds) and Resend (email, optional).

---

## Prerequisites

- **.NET SDK 10** and **Node.js 20+** (npm 10+) to run from source
- **Docker** and **Docker Compose v2** if you prefer containers, or for a throwaway Postgres and Keycloak
- A **PostgreSQL** database you can create a schema in
- An **Anthropic API key**. Without one the Engine falls back to a mock LLM client, which is fine for UI work and produces no real code
- At least **4 GB RAM**: the Compile Guarantee runs `dotnet build`, `npm ci` and `next build` on the same host

---

## Quick Start: Demo Mode

Demo mode is the fastest way to see the UI. With no Supabase URL set outside production, the web app enables demo mode automatically: no sign-in and no database.

```bash
git clone https://github.com/stevenfackley/StackAlchemist.git
cd StackAlchemist
npm install                                  # also creates .env from .env.example
dotnet run --project src/StackAlchemist.Engine     # terminal A, :5000
npm run dev --prefix src/StackAlchemist.Web        # terminal B, :3000
```

Demo mode does not store anything, so generations do not persist across restarts.

---

## Full Setup: Postgres and Keycloak

### 1. Database

The platform keeps its data in a Postgres schema named `stackalchemist`. The web app uses Drizzle over postgres-js and the Engine uses Npgsql. Tables are `profiles`, `generations`, `transactions` and `stripe_events`, plus a few database functions and triggers. The migrations live in `src/StackAlchemist.Web/drizzle/`.

There is no row-level security. Every query is scoped to the signed-in user in application code, so connect the app with its own role and do not expose the database to anything else.

Start a throwaway Postgres and apply the migrations:

```bash
docker run -d --name sa-pg -p 55440:5432 \
  -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=stackalchemist \
  postgres:17-alpine

cd src/StackAlchemist.Web
DATABASE_URL_MIGRATE=postgres://postgres:postgres@127.0.0.1:55440/stackalchemist npm run db:migrate
```

In PowerShell, set `$env:DATABASE_URL_MIGRATE = '...'` first. Re-running is a no-op. `npm run db:migrate` is the only supported way to apply the schema; do not use `drizzle-kit migrate` or `push`.

Percent-encode special characters in the password (`#` as `%23`, `@` as `%40`, `$` as `%24`).

### 2. Keycloak realm

Sign-in goes through a Keycloak realm using Auth.js. The app expects:

- an OIDC client named `<realm>-web` (for the default realm, `stackalchemist-web`)
- a public client with PKCE (`S256`), standard flow only
- redirect URI `https://<your-origin>/api/auth/callback/keycloak` (`http://localhost:3000/api/auth/callback/keycloak` locally)
- the same origin in the client's web origins and post-logout redirect list
- user registration enabled if you want self-signup (the app's `/register` uses `prompt=create`, which needs Keycloak 26.1 or newer)

Realm definitions for the hosted platform live in a separate repository, so create an equivalent realm yourself. Users need a first and last name or Keycloak stops them at a required-action screen.

The details, including a local walkthrough, are in `docs/runbooks/qavren-auth.md`.

### 3. Environment

Put these in `src/StackAlchemist.Web/.env.local` for the web app (Next reads env files from the web project directory):

```env
DATABASE_URL=postgres://postgres:postgres@127.0.0.1:55440/stackalchemist
QAVREN_AUTH_URL=http://localhost:8090      # Keycloak base URL, no realm path
QAVREN_REALM=stackalchemist                # optional, this is the default
AUTH_SECRET=...                            # openssl rand -base64 32
NEXT_PUBLIC_DEMO_MODE=false
NEXT_PUBLIC_APP_URL=http://localhost:3000
ENGINE_API_URL=http://localhost:5000
ENGINE_SERVICE_KEY=...                     # same value in the Engine's environment
```

If `QAVREN_AUTH_URL` is set, the web server refuses to start unless `DATABASE_URL` and `AUTH_SECRET` are also set and the URL is an absolute http(s) URL. Behind a reverse proxy also set `AUTH_URL` to the public origin only (no path), and bake `NEXT_PUBLIC_APP_URL` into the web image at build time; sign-out compares the request's origin against it.

The Engine reads its configuration from environment variables (see `.env.example` for every key):

```env
DATABASE_URL=postgres://...                # same database
ANTHROPIC_API_KEY=sk-ant-...
ENGINE_SERVICE_KEY=...                     # shared secret, X-Engine-Key header
BYOK_ENCRYPTION_KEY=...                    # 32+ chars, distinct from ENGINE_SERVICE_KEY
R2_ACCOUNT_ID=...
R2_ACCESS_KEY_ID=...
R2_SECRET_ACCESS_KEY=...
R2_BUCKET_NAME=...
STRIPE_SECRET_KEY=sk_...
STRIPE_WEBHOOK_SECRET=whsec_...
RESEND_API_KEY=                            # optional; blank disables email
```

Optional: `ANTHROPIC_MODEL` overrides the model. The default is Claude Sonnet 5.5 (`claude-sonnet-5-5`). `ANTHROPIC_EFFORT` sets the reasoning effort (default `medium`).

In `ASPNETCORE_ENVIRONMENT=Production` the Engine refuses to start without `ENGINE_SERVICE_KEY` and a data store (`DATABASE_URL`).

### 4. Run

```bash
dotnet run --project src/StackAlchemist.Engine
npm run dev --prefix src/StackAlchemist.Web
```

---

## Docker

The repository ships a multi-stage `Dockerfile` with `web`, `engine` and `worker` targets. `docker-compose.yml` builds `sa-web`, `sa-engine` and `sa-worker` for local use and reads its environment from the repo-root `.env` (copy `.env.example`, or run `node scripts/setup-env.mjs`). It does not include Postgres or Keycloak, so point `DATABASE_URL` and `QAVREN_AUTH_URL` at ones you run yourself. You do not need `sa-worker`; start only the two services you need:

```bash
docker compose up sa-web sa-engine
```

For a production-style layout, `docker-compose.prod.yml` shows the shape the hosted platform uses: an nginx `reverse-proxy`, `sa-web` and `sa-engine` on one network, with the Cloudflare Tunnel run separately. It is written for that specific host (env names, the `stackalchemist-prod` network, a Cloudflare Tunnel in front), so treat it as a reference rather than a drop-in file. Build the images through compose so `NEXT_PUBLIC_APP_URL` is passed as a build argument; a bare `docker build` bakes in a default URL that is wrong for your site.

### Reverse proxy

Route three paths. `/api/auth/` must reach the web app, not the Engine, or the OAuth callback fails.

```nginx
server {
    listen 443 ssl;
    server_name stackalchemist.yourdomain.com;

    location /api/auth/ {            # Auth.js
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location = /api/healthz {        # web health probe
        proxy_pass http://localhost:3000;
    }

    location /api/ {                 # Engine API
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
    }

    location / {                     # frontend
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

There is no WebSocket traffic. The generation status page polls the server instead of holding a connection open. `docker/nginx.prod.conf` has the full production version with security headers and a maintenance page.

---

## Build Workspace

The Compile Guarantee builds each generated project in a temporary directory on the Engine host, using the host's `dotnet`, `node`/`npm` and `python` (for the FastAPI stack, a per-build virtualenv). The Engine image includes those toolchains. If you run the Engine outside Docker, install them yourself: .NET 10 SDK, Node.js, and Python with `flake8` and `pytest` available.

The Engine does not start containers for builds and does not need the Docker socket. Generated code is untrusted, so run the Engine on a machine or VM you are happy to have executing `npm` and `pip` installs.

---

## Monitoring

- Engine: `GET /healthz`
- Web: `GET /api/healthz` (exempt from auth)

```bash
curl http://localhost:5000/healthz
curl http://localhost:3000/api/healthz
docker compose logs -f sa-engine
```

---

## Legacy Supabase Mode

Earlier versions used Supabase for auth and data. The hosted platform left it on 2026-10-01, and the legacy "Supabase mode" is being removed from the code. Do not set up new installs on Supabase.

---

## Known Limitations in Self-Hosted Mode

- **You supply every service.** Anthropic, R2, Stripe, Resend, Postgres and Keycloak are yours to configure and pay for.
- **No managed refunds.** Automatic refunds go through your own Stripe account and webhook, which you must set up.
- **No SLA.** Uptime is your responsibility.
- **Template updates.** `git pull` to get new templates and fixes.
- **Licensing.** The license limits you to non-commercial, evaluation and personal use, and does not allow hosting it as a public service.

---

## Getting Help

- **GitHub Issues:** [github.com/stevenfackley/StackAlchemist/issues](https://github.com/stevenfackley/StackAlchemist/issues)
- **Docs:** [stackalchemist.app/docs](https://stackalchemist.app/docs)

---

## Related Docs

- [Architecture Overview →](./architecture-overview)
- [The Compile Guarantee →](./compile-guarantee)
- [Getting Started →](../user/getting-started)
