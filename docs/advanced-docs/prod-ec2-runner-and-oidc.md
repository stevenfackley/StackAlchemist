# Production EC2 Runner + GitHub OIDC Setup

This runbook documents the agreed production deployment approach for StackAlchemist:

- **AWS EC2 host** in `us-east-1`
- **Docker-based production runtime**
- **Cloudflare Tunnel** running in Docker
- **nginx reverse proxy** in front of `sa-web` and `sa-engine`
- **GitHub Actions self-hosted runner** on the EC2 host
- **GitHub OIDC** enabled for future AWS-integrated automation

---

## 1. Production topology

Production runs these containers:

- `reverse-proxy` (nginx)
- `sa-web`
- `sa-engine`
- `cloudflared`

Public routing:

- `https://stackalchemist.app/` → `sa-web`
- `https://stackalchemist.app/api/*` → `sa-engine`

---

## 2. Self-hosted runner recommendation

The production deploy workflow is designed to run **on the EC2 host itself**.

Why this is the recommended approach:

- avoids SSH-based deployments
- gives the workflow direct local Docker access
- keeps ARM64 compatibility simple on the `t4g.small` instance
- minimizes moving parts for the first production rollout

Recommended runner labels:

- `self-hosted`
- `linux`
- `ARM64`
- `prod`

---

## 3. EC2 host prerequisites

Before registering the runner, make sure the host has:

- Docker Engine installed
- Docker Compose v2 available via `docker compose`
- a non-root user that can run Docker commands
- enough disk space for image builds
- the repository checked out in a stable path

Recommended application path:

- `/home/stack-alchemist/StackAlchemist`

---

## 4. Install the GitHub Actions runner on EC2

On the EC2 host:

1. Create a dedicated runner directory.
2. Download the Linux ARM64 runner package from GitHub Actions.
3. Configure it against this repository.
4. Apply the labels: `self-hosted,linux,ARM64,prod`.
5. Install and start it as a service.

At a high level, the flow looks like:

```bash
mkdir -p /home/stack-alchemist/actions-runner
cd /home/stack-alchemist/actions-runner

# Download the Linux ARM64 runner tarball from GitHub's official release URL
# then extract it

./config.sh --url https://github.com/stevenfackley/StackAlchemist --token <RUNNER_REGISTRATION_TOKEN> --labels self-hosted,linux,ARM64,prod
sudo ./svc.sh install
sudo ./svc.sh start
```

> Use a **runner registration token** from GitHub when configuring the runner. This is different from repository secrets and different from AWS credentials.

---

## 5. GitHub OIDC setup walkthrough

OIDC lets GitHub Actions obtain **temporary AWS credentials** without storing long-lived AWS keys in GitHub.

### Why use it here

Even though the deploy runs locally on the EC2 runner today, OIDC is still the best future-proof setup for:

- AWS Systems Manager access
- ECR usage later if needed
- CloudWatch / infrastructure automation
- any future AWS API calls added to the workflow

### What you create in AWS

1. Add GitHub as an **IAM Identity Provider** using:
   - Provider URL: `https://token.actions.githubusercontent.com`
   - Audience: `sts.amazonaws.com`
2. Create an **IAM role** trusted by that identity provider.
3. Restrict the trust policy to this repository (and optionally specific branch/tag patterns).
4. Attach only the permissions the workflow actually needs.

### Example trust policy shape

Replace the repository name only if it ever changes:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Federated": "arn:aws:iam::<AWS_ACCOUNT_ID>:oidc-provider/token.actions.githubusercontent.com"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "token.actions.githubusercontent.com:aud": "sts.amazonaws.com"
        },
        "StringLike": {
          "token.actions.githubusercontent.com:sub": [
            "repo:stevenfackley/StackAlchemist:ref:refs/heads/main",
            "repo:stevenfackley/StackAlchemist:environment:prod"
          ]
        }
      }
    }
  ]
}
```

### What to store in GitHub after OIDC is configured

Store the IAM role ARN as a repository or environment variable/secret, for example:

- `AWS_ROLE_TO_ASSUME`

Then future workflow steps can use `aws-actions/configure-aws-credentials`.

---

## 6. Production secrets expected in GitHub

Environment: **`prod`**

Expected values already discussed in this repo:

- `SUPABASE_URL`
- `SUPABASE_ANON_KEY`
- `SUPABASE_SERVICE_ROLE_KEY`
- `R2_ACCESS_KEY_ID`
- `R2_SECRET_ACCESS_KEY`
- `R2_PUBLIC_URL`
- `STRIPE_PUBLISHABLE_KEY`
- `STRIPE_SECRET_KEY`
- `STRIPE_WEBHOOK_SECRET`
- `ANTHROPIC_API_KEY`
- `CLOUDFLARE_TUNNEL_TOKEN`
- optional: `ENGINE_SERVICE_KEY`

Repository-level:

- `CF_ACCOUNT_ID`

---

## 7. Production workflow behavior

The production workflow:

- triggers on pushes to the `main` branch
- can also be started manually with `workflow_dispatch`
- automatically updates `CHANGELOG.md` and creates a GitHub Release record
- generates a `.env` file from GitHub secrets
- validates `docker-compose.prod.yml`
- builds and replaces the production stack via `docker compose -f docker-compose.prod.yml up -d`
- performs post-deploy health checks and site verification
- prints container status after deploy

This keeps the deployment fully Docker-based and avoids SSH-driven release steps.

---

## 8. Remaining manual tasks

Before the first real production deployment, complete these items:

1. Install and register the self-hosted runner on the EC2 host.
2. Make sure the runner user can run Docker commands.
3. Verify the production Cloudflare Tunnel is configured to route `stackalchemist.app` to the nginx reverse proxy service.
4. Confirm the production secrets in GitHub are correct.
5. Optionally create the AWS OIDC provider + IAM role for future workflow AWS API access.

### Cloudflare Tunnel — long-lived standalone container (required one-time setup)

`cloudflared` is **not** managed by `docker-compose.prod.yml`. It is a long-lived
standalone container (`sa-tunnel`) that survives every deploy. This prevents
Cloudflare Error 1033 that used to occur because every compose-up killed and
recreated the tunnel.

**One-time setup on the EC2 host** (run once after the first deploy):

```bash
# Remove any old tunnel container that compose may have managed
docker rm -f sa-tunnel 2>/dev/null || true

# Start the standalone long-lived tunnel
docker run -d \
  --name sa-tunnel \
  --restart unless-stopped \
  cloudflare/cloudflared:latest \
  tunnel --no-autoupdate run --token <CLOUDFLARE_TUNNEL_TOKEN>

# Connect it to the prod network (the deploy workflow reconnects it on every deploy)
docker network connect stackalchemist-prod sa-tunnel
```

Replace `<CLOUDFLARE_TUNNEL_TOKEN>` with the value from the `CLOUDFLARE_TUNNEL_TOKEN`
GitHub secret (or the `.env` file in `$DEPLOY_DIR`).

The deploy workflow (`deploy-prod.yml`) automatically runs
`docker network connect stackalchemist-prod sa-tunnel` after each `compose up`
because compose recreates the `stackalchemist-prod` network, which disconnects
any external containers.

**Add swap to prevent OOM-induced runner failures** (one-time, also on EC2 host):

```bash
sudo fallocate -l 4G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

The `t4g.small` instance has only 2 GB RAM. Without swap, Docker builds for
Next.js + .NET can OOM-kill the GitHub Actions runner process mid-deploy.

### Cloudflare Tunnel checklist for production

What still must exist in Cloudflare is the **tunnel-side hostname mapping**.

In the Cloudflare Zero Trust dashboard, the production tunnel should have a public hostname with these values:

- **Hostname:** `stackalchemist.app`
- **Service type:** `HTTP`
- **URL / Service:** `http://sa-reverse-proxy:80` _(or `http://reverse-proxy:80` — both resolve)_

Optional additional hostname:

- **Hostname:** `www.stackalchemist.app`
- **Service type:** `HTTP`
- **URL / Service:** `http://sa-reverse-proxy:80`

> **Why both hostnames work:** `docker-compose.prod.yml` attaches a network alias
> `reverse-proxy` to the `sa-reverse-proxy` container on the `stackalchemist-prod`
> network. The deploy workflow does the same for the maintenance-page container
> via `--network-alias reverse-proxy`. This keeps the tunnel ingress working
> regardless of whether the CF dashboard points at the container_name or the
> alias, so future renames in compose don't silently break production. If you
> ever need to change the nginx container name again, update the alias list in
> `docker-compose.prod.yml` and the `--network-alias` flag in `deploy-prod.yml`
> together.

If you manage the tunnel from the host with `cloudflared tunnel route dns`, the effective intent is the same: bind the production tunnel to `stackalchemist.app` and forward traffic to the internal reverse proxy.

After saving the hostname, verify all of the following:

1. `stackalchemist.app` is proxied by Cloudflare DNS.
2. SSL mode is **Full (Strict)**.
3. The tunnel token stored in GitHub `prod` secrets matches that exact production tunnel.
4. The first production deploy brings up `reverse-proxy`, `sa-web`, `sa-engine`, and `sa-tunnel` (standalone) successfully.
5. `https://stackalchemist.app/` loads the frontend and `https://stackalchemist.app/api/health` responds from the engine.

---

## 9. Notes on service scope

The first production rollout intentionally excludes a standalone `sa-worker` container.

If compile/build workload behavior later requires scale-out or stronger isolation, reintroduce `sa-worker` as its own service and update the production compose + workflow accordingly.