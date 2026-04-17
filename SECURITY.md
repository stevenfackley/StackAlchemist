# Security

## Reporting

Report suspected vulnerabilities privately via GitHub Security Advisories on this repo, or email `{{AUTHOR}}` directly. Do not open public issues for undisclosed vulns.

## Response

- Acknowledge within 72h.
- Fix targeted for the next release cut; out-of-band if critical.

## Rules

- No static AWS keys. OIDC only.
- No telemetry SDKs (ApplicationInsights / Sentry / Datadog / etc). Enforced by CI.
- Secret-shape grep runs on every PR; PEM, `sk_live_`, `ghp_`, AKIA, etc. will fail the build.
- `.env` files are chmod 600 on deploy targets; content is committed only as `.env.example` / `.env.dev` / `.env.test`.
