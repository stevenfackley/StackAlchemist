/**
 * supabase-auth-smtp.mjs — configure and verify Supabase Auth custom SMTP
 * ─────────────────────────────────────────────────────────────────────────
 *
 * WHY THIS EXISTS
 * ───────────────
 * Supabase Auth does not send magic links, signup confirmations or password
 * resets through the Engine's Resend HTTP client. It has its own mailer,
 * configured on the Supabase project. With no custom SMTP server attached it
 * falls back to Supabase's built-in mailer, which is capped at 2 emails per
 * hour PROJECT-WIDE and documented as best-effort only.
 *
 * That cap is not a theoretical risk: on 2026-07-11 magic-link sign-in was
 * reported broken for days. The links were being rate-limited into the void.
 *
 * The fix is operator-side, not code-side — which is exactly why it kept
 * getting deferred and why nothing in CI noticed. This script gives that
 * operator step a checked-in, reviewable, repeatable form:
 *
 *   verify (default) — assert the live project actually has custom SMTP on.
 *                      Safe, read-only, no secrets required beyond the token.
 *   apply            — PATCH the project's auth config to point at the SMTP
 *                      relay and raise the email rate limit.
 *
 * The human click-path, the DNS prerequisites and the rollback are documented
 * in docs/runbooks/supabase-auth-smtp.md. Read that first — this script is the
 * automation of a procedure, not a replacement for understanding it.
 *
 * WHAT THIS SCRIPT DELIBERATELY DOES NOT DO
 * ─────────────────────────────────────────
 *  ✗ It does not mint, store or read API keys from disk. Every secret arrives
 *    as an environment variable for the duration of one run.
 *  ✗ It does not print the SMTP password, or echo it back from the API, under
 *    any flag — including --dry-run.
 *  ✗ It is not wired into CI. Applying auth config is a deliberate, audited
 *    act; a workflow that can silently repoint the mailer is a phishing
 *    primitive, not a convenience.
 *
 * USAGE
 * ─────
 *   node scripts/supabase-auth-smtp.mjs                # verify prod
 *   node scripts/supabase-auth-smtp.mjs verify
 *   node scripts/supabase-auth-smtp.mjs apply --dry-run
 *   node scripts/supabase-auth-smtp.mjs apply
 *
 * ENVIRONMENT
 * ───────────
 *   SUPABASE_ACCESS_TOKEN     required — https://supabase.com/dashboard/account/tokens
 *   SUPABASE_PROJECT_REF      optional — defaults to the prod ref below
 *   SUPABASE_AUTH_SMTP_PASS   required for `apply` — the SMTP password
 *                             (for Resend: an API key beginning `re_`)
 *
 * Optional overrides, all defaulted for the prod relay:
 *   SUPABASE_AUTH_SMTP_HOST, SUPABASE_AUTH_SMTP_PORT, SUPABASE_AUTH_SMTP_USER,
 *   SUPABASE_AUTH_SMTP_SENDER, SUPABASE_AUTH_SMTP_SENDER_NAME,
 *   SUPABASE_AUTH_EMAIL_RATE_LIMIT
 *
 * EXIT CODES
 * ──────────
 *   0  verify passed / apply succeeded
 *   1  verify found the project on the built-in mailer, or a check failed
 *   2  bad invocation — missing env var, unknown subcommand
 */

const API_ROOT = "https://api.supabase.com/v1";

/**
 * Prod project ref, from the 2026-07-11 auth incident:
 * "StackAlchemist-Production" (West US). Overridable so the same script can
 * be pointed at the CI project (cdlefpvsvyepofsboepc) or a branch preview.
 */
const DEFAULT_PROJECT_REF = "ctqhwykryoglhdwatljt";

/**
 * Resend's SMTP relay. The username is the literal string "resend" for every
 * account — it is not the account email, and it is not the API key. The API
 * key is the PASSWORD. Getting these two backwards produces a 535 auth error
 * that Supabase surfaces only as a generic "Error sending magic link email".
 */
const DEFAULTS = {
  host: "smtp.resend.com",
  port: 587,
  user: "resend",
  // Matches RESEND_FROM_EMAIL in .env.example so users see one consistent
  // sender across auth mail and Engine mail. Resend verifies the DOMAIN, so
  // any address at stackalchemist.app works once DNS is in place.
  sender: "noreply@stackalchemist.app",
  senderName: "StackAlchemist",
  /**
   * Emails per hour, project-wide. Supabase only honours a custom value once
   * custom SMTP is enabled — on the built-in mailer it is pinned at 2. 100 is
   * comfortably above organic signup volume and still low enough that a
   * credential-stuffing run against /auth/v1/otp cannot drain the Resend
   * quota in an afternoon.
   */
  emailRateLimit: 100,
};

const args = process.argv.slice(2);
const command = args.find((a) => !a.startsWith("-")) ?? "verify";
const dryRun = args.includes("--dry-run");

/** Exits with a usage error. Reserved for operator mistakes, not API failures. */
function usageError(message) {
  console.error(`ERROR: ${message}`);
  console.error("\nSee docs/runbooks/supabase-auth-smtp.md");
  process.exit(2);
}

function requireEnv(name, hint) {
  const value = process.env[name];
  if (!value) usageError(`${name} is not set. ${hint}`);
  return value;
}

/**
 * Shows enough of a secret to confirm you grabbed the right one, and not
 * enough to use it. Resend keys are `re_` + ~30 chars, so the prefix alone
 * distinguishes "wrong key" from "wrong kind of value entirely".
 */
function fingerprint(secret) {
  if (!secret) return "(unset)";
  if (secret.length <= 8) return "(too short to be a valid key)";
  return `${secret.slice(0, 5)}…${secret.slice(-2)} (${secret.length} chars)`;
}

async function callApi(method, path, body) {
  const token = requireEnv(
    "SUPABASE_ACCESS_TOKEN",
    "Create one at https://supabase.com/dashboard/account/tokens",
  );

  const response = await fetch(`${API_ROOT}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    const text = await response.text();
    // The auth config payload echoes secrets on some error paths, so the body
    // is truncated rather than dumped wholesale.
    throw new Error(
      `${method} ${path} -> ${response.status} ${response.statusText}: ${text.slice(0, 300)}`,
    );
  }

  return response.json();
}

const projectRef = process.env.SUPABASE_PROJECT_REF || DEFAULT_PROJECT_REF;

async function verify() {
  console.log(`Project: ${projectRef}`);
  const config = await callApi("GET", `/projects/${projectRef}/config/auth`);

  // Supabase reports custom SMTP as configured when a host is present. The
  // password is never returned by the API, so its absence proves nothing —
  // host is the only honest signal.
  const host = config.smtp_host ?? null;
  const customSmtpOn = Boolean(host);

  console.log(`  Custom SMTP host   : ${host ?? "(none — built-in mailer)"}`);
  console.log(`  SMTP port          : ${config.smtp_port ?? "(unset)"}`);
  console.log(`  SMTP user          : ${config.smtp_user ?? "(unset)"}`);
  console.log(`  Sender address     : ${config.smtp_admin_email ?? "(unset)"}`);
  console.log(`  Sender name        : ${config.smtp_sender_name ?? "(unset)"}`);
  console.log(`  Emails per hour    : ${config.rate_limit_email_sent ?? "(unset)"}`);
  console.log(`  Email signin       : ${config.external_email_enabled ? "enabled" : "DISABLED"}`);

  const problems = [];

  if (!customSmtpOn) {
    problems.push(
      "No custom SMTP host. Supabase Auth is on the built-in mailer: 2 emails/hour " +
        "project-wide, best-effort delivery. Magic links WILL be dropped.",
    );
  }

  // A rate limit still sitting at the built-in default means the limit was
  // never raised after SMTP was attached — the classic half-done migration,
  // where delivery works in testing and then silently caps in production.
  if (customSmtpOn && Number(config.rate_limit_email_sent ?? 0) <= 2) {
    problems.push(
      `Custom SMTP is on but rate_limit_email_sent is ${config.rate_limit_email_sent}. ` +
        "The built-in cap is still in force; raise it or SMTP buys you nothing.",
    );
  }

  if (!config.external_email_enabled) {
    problems.push(
      "external_email_enabled is false — email sign-in is off entirely, so the " +
        "login page's magic-link form cannot work regardless of SMTP.",
    );
  }

  if (problems.length > 0) {
    console.error("");
    for (const problem of problems) console.error(`ERROR: ${problem}`);
    console.error("\nRemediation: docs/runbooks/supabase-auth-smtp.md");
    process.exit(1);
  }

  console.log("\nOK — custom SMTP is configured and the email rate limit is raised.");
}

async function apply() {
  const pass = requireEnv(
    "SUPABASE_AUTH_SMTP_PASS",
    "This is the SMTP password (for Resend, an API key beginning `re_`). " +
      "Set it in your shell for this run only — never commit it.",
  );

  const payload = {
    external_email_enabled: true,
    smtp_host: process.env.SUPABASE_AUTH_SMTP_HOST || DEFAULTS.host,
    smtp_port: Number(process.env.SUPABASE_AUTH_SMTP_PORT || DEFAULTS.port),
    smtp_user: process.env.SUPABASE_AUTH_SMTP_USER || DEFAULTS.user,
    smtp_pass: pass,
    smtp_admin_email: process.env.SUPABASE_AUTH_SMTP_SENDER || DEFAULTS.sender,
    smtp_sender_name: process.env.SUPABASE_AUTH_SMTP_SENDER_NAME || DEFAULTS.senderName,
    rate_limit_email_sent: Number(
      process.env.SUPABASE_AUTH_EMAIL_RATE_LIMIT || DEFAULTS.emailRateLimit,
    ),
  };

  console.log(`Project: ${projectRef}`);
  console.log("Payload:");
  for (const [key, value] of Object.entries(payload)) {
    console.log(`  ${key.padEnd(22)}: ${key === "smtp_pass" ? fingerprint(value) : value}`);
  }

  if (dryRun) {
    console.log("\n--dry-run: nothing sent.");
    return;
  }

  await callApi("PATCH", `/projects/${projectRef}/config/auth`, payload);
  console.log("\nApplied. Re-reading to confirm…\n");
  await verify();

  console.log(
    "\nNext: send yourself a magic link and confirm the mail arrives from the " +
      "new sender. A green config is not a delivered email — DNS (SPF/DKIM) " +
      "has to be verified at the relay too. See the runbook.",
  );
}

const commands = { verify, apply };

if (!Object.hasOwn(commands, command)) {
  usageError(`Unknown command "${command}". Expected: verify | apply`);
}

try {
  await commands[command]();
} catch (error) {
  console.error(`ERROR: ${error.message}`);
  process.exit(1);
}
