# Self-Hosting StackAlchemist

StackAlchemist is source-available under a Proprietary & Source Available license. Personal use and evaluation are free. Commercial production use requires a purchased license at [stackalchemist.app](https://stackalchemist.app).

This guide covers running StackAlchemist on your own infrastructure.

---

## License Summary

| Use Case | License Required |
|----------|-----------------|
| Personal/evaluation use, learning | Free — no license |
| Internal tools (non-commercial) | Free — no license |
| Commercial SaaS built on StackAlchemist | Purchased commercial license |
| Distributing StackAlchemist itself | Not permitted without written agreement |

For commercial licensing inquiries, contact the team at [stackalchemist.app](https://stackalchemist.app).

---

## Prerequisites

Before self-hosting, ensure you have:

- **Docker** and **Docker Compose** (v2+) installed
- A **Supabase project** (free tier is sufficient for self-hosting/personal use)
- An **Anthropic API key** (Claude 3.5 Sonnet for LLM injection)
- A server or local machine with at least **4GB RAM** (build containers are memory-intensive)

---

## Quick Start with Docker Compose

### 1. Clone the Repository

```bash
git clone https://github.com/stevenfackley/StackAlchemist.git
cd StackAlchemist
```

### 2. Configure Environment Variables

```bash
cp .env.example .env
```

Open `.env` and fill in the required values (see [Environment Variables](#environment-variables) below).

### 3. Start the Stack

```bash
docker compose up
```

This starts:
- **Frontend** (Next.js) on `http://localhost:3000`
- **API** (.NET) on `http://localhost:5000`
- **Worker** (.NET Worker Service) — background job processor
- **PostgreSQL** on `localhost:5432`

---

## Environment Variables

### Required

```env
# ─── Supabase ─────────────────────────────────────────────
# Get these from your Supabase project → Settings → API
NEXT_PUBLIC_SUPABASE_URL=https://your-project.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=your-anon-key
SUPABASE_SERVICE_ROLE_KEY=your-service-role-key

# ─── Anthropic ────────────────────────────────────────────
# Get from console.anthropic.com
ANTHROPIC_API_KEY=sk-ant-...

# ─── Database ─────────────────────────────────────────────
# Used by the API and Worker (not the same as Supabase hosted DB)
DATABASE_URL=postgresql://postgres:password@postgres:5432/stackalchemist

# ─── Internal ─────────────────────────────────────────────
NEXT_PUBLIC_API_URL=http://localhost:5000
```

### Optional

```env
# Override Claude model (default: claude-3-5-sonnet-20241022)
ANTHROPIC_MODEL=claude-3-5-sonnet-20241022

# Maximum retries for compile correction loop (default: 3)
COMPILE_MAX_RETRIES=3

# Build container memory limit in MB (default: 2048)
BUILD_CONTAINER_MEMORY_MB=2048

# Log level: debug | info | warn | error (default: info)
LOG_LEVEL=info
```

---

## Supabase Setup

StackAlchemist uses Supabase for authentication and real-time progress updates. You need to configure your Supabase project before running the platform.

### 1. Create a Supabase Project

Go to [supabase.com](https://supabase.com) and create a new project.

### 2. Run the Database Migrations

From the Supabase SQL Editor, run the platform migrations in order:

```bash
# Or using psql against your Supabase database
psql $DATABASE_URL -f platform/migrations/001_auth_schema.sql
psql $DATABASE_URL -f platform/migrations/002_generation_jobs.sql
psql $DATABASE_URL -f platform/migrations/003_rls_policies.sql
```

### 3. Configure Auth

In your Supabase project → Authentication → URL Configuration:

- Site URL: `http://localhost:3000` (or your production domain)
- Redirect URLs: `http://localhost:3000/auth/callback`

### 4. Enable Storage

In Supabase Storage, create a bucket named `generated-archives` with the following policy:

```sql
-- Only the authenticated user can download their own archives
CREATE POLICY "user_download_own_archive"
ON storage.objects FOR SELECT
USING (
  auth.uid()::text = (storage.foldername(name))[1]
);
```

---

## Building from Source

If you want to build the Docker images locally rather than using pre-built images:

```bash
# Build all services
docker compose -f docker-compose.yml build

# Build specific service
docker compose build api
docker compose build worker
docker compose build web
```

Or build individual .NET projects:

```bash
# API
cd src/StackAlchemist.Placeholder
dotnet build

# Worker
cd src/StackAlchemist.Worker
dotnet build

# Run tests
cd src/StackAlchemist.Engine.Tests
dotnet test

cd src/StackAlchemist.Worker.Tests
dotnet test
```

---

## Production Deployment

For production self-hosting, use `docker-compose.prod.yml`:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

The production compose file includes:
- Resource limits per service
- Restart policies (`unless-stopped`)
- Health checks for API and Worker
- Reduced log verbosity

### Reverse Proxy (nginx example)

```nginx
server {
    listen 443 ssl;
    server_name stackalchemist.yourdomain.com;

    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    location /api/ {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
    }

    location /ws/ {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

---

## Build Containers

The Compile Guarantee requires the ability to spin up temporary Docker containers to run `dotnet build` and `npm run build`. On a self-hosted instance, the Worker service must have access to the Docker daemon.

The Docker Compose configuration mounts the Docker socket:

```yaml
worker:
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock
```

**Security note:** Mounting the Docker socket gives the Worker container root-equivalent access to the host. On a shared or multi-tenant host, run the Worker on a dedicated machine or inside a VM.

---

## Monitoring

The Worker exposes a health endpoint at `http://worker:8080/health`. The API exposes its health at `http://api:5000/health`.

For basic monitoring:

```bash
# Check all service health
curl http://localhost:5000/health
curl http://localhost:8080/health

# View logs
docker compose logs -f worker
docker compose logs -f api
```

---

## Known Limitations in Self-Hosted Mode

- **No managed billing** — You're responsible for your own Anthropic API costs
- **No managed refunds** — The Compile Guarantee refund automation requires the hosted platform's payment integration
- **No SLA** — The hosted platform provides uptime guarantees; self-hosted is your responsibility
- **Template updates** — You'll need to `git pull` to receive updated templates as new frameworks and patterns are added

---

## Getting Help

- **GitHub Issues:** [github.com/stevenfackley/StackAlchemist/issues](https://github.com/stevenfackley/StackAlchemist/issues)
- **Docs:** [stackalchemist.app/docs](https://stackalchemist.app/docs)

---

## Related Docs

- [Architecture Overview →](./architecture-overview)
- [The Compile Guarantee →](./compile-guarantee)
- [Getting Started →](../user/getting-started)
