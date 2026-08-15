# {{ProjectName}} Deployment Runbook

This runbook is generated for Tier 3 deliveries. It assumes the application zip contains generated app code plus `infra/` deployment assets.

## Included Assets

- `infra/cdk/`: AWS CDK TypeScript stack for VPC, ECS Fargate, ALB, and PostgreSQL.
- `infra/terraform/`: Terraform AWS baseline for ECS, ALB, RDS, networking, and service logs.
- `infra/helm/`: Kubernetes chart for a containerized deployment behind a service and optional ingress.
- Existing Docker Compose assets remain the fastest local validation path before cloud deployment.

## Application Shape

- Project: `{{ProjectName}}`
- Slug: `{{ProjectNameKebab}}`
- Default frontend URL: `{{FrontendUrl}}`
- Entities:
{{#each Entities}}
  - `{{Name}}` stored in `{{TableName}}`
{{/each}}

## Preflight

1. Build and run the generated app locally with Docker Compose.
2. Set production secrets in your target platform secret store; do not commit `.env` files.
3. Build and push an application image tagged with the same version you deploy.
4. Run database migrations before routing public traffic to the new service.
5. Keep rollback image tags and database backups for the deployment window.

## AWS CDK

The app entrypoint is `infra/cdk/bin/app.ts`; it instantiates `{{ProjectName}}Stack` from
`infra/cdk/lib/{{ProjectNameKebab}}-stack.ts`. `cdk.json` points the CLI at it.

```bash
cd infra/cdk
npm ci
npm run synth -- --context imageUri=<account>.dkr.ecr.<region>.amazonaws.com/{{ProjectNameKebab}}:<tag>
npm run deploy -- --context imageUri=<account>.dkr.ecr.<region>.amazonaws.com/{{ProjectNameKebab}}:<tag>
```

`imageUri` is required, not optional: the stack raises `Missing CDK context value: imageUri`
rather than synthesising a template that points at a placeholder image.

**Synth needs no AWS credentials.** With none configured the CLI leaves `CDK_DEFAULT_ACCOUNT`
and `CDK_DEFAULT_REGION` unset, so `bin/app.ts` builds an environment-agnostic stack and the
CloudFormation template lands in `cdk.out/{{ProjectName}}Stack.template.json`. Review it there
before you have an account wired up. Deployment does need credentials, and pinning the target
explicitly is worth doing:

```bash
export CDK_DEFAULT_ACCOUNT=<account>
export CDK_DEFAULT_REGION=<region>
npx cdk bootstrap   # once per account/region
```

The CDK stack creates managed AWS resources with secure defaults, but you should review sizing, region, tags, and deletion policies before production use.

## Terraform

```bash
cd infra/terraform
terraform init
terraform plan -var="project_name={{ProjectNameKebab}}" -var="image_uri=<image-uri>"
terraform apply -var="project_name={{ProjectNameKebab}}" -var="image_uri=<image-uri>"
```

Store Terraform state remotely before team use.

## Helm

```bash
helm upgrade --install {{ProjectNameKebab}} ./infra/helm \
  --namespace {{ProjectNameKebab}} \
  --create-namespace \
  --set image.repository=<image-repository> \
  --set image.tag=<tag> \
  --set secrets.databaseUrl='<database-url>'
```

Use external secret management in production instead of direct `--set secrets.*` values when available.

## Rollback

1. Re-point the service to the previous container image tag.
2. Confirm health checks and smoke tests pass.
3. Restore the database only if a migration caused data corruption and no forward fix is safe.
4. Record the failed version, logs, and remediation in the project incident notes.
