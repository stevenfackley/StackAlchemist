/**
 * setup-env.mjs — StackAlchemist environment bootstrap script
 * ─────────────────────────────────────────────────────────────
 *
 * PURPOSE
 * -------
 * This script creates a local `.env` file at the solution root the first time
 * a developer clones the repo.  It is intentionally simple: it copies
 * `.env.example` → `.env` if `.env` does not already exist.
 *
 * WHY A SINGLE .env FILE?
 * ────────────────────────
 * StackAlchemist uses a single `.env` file (not `.env.development`,
 * `.env.production`, etc.) because:
 *
 *  1. The same set of secrets (API keys, DB passwords) are needed by THREE
 *     separate processes: Next.js frontend, .NET Engine API, and Docker Compose.
 *     A single `.env` is the only file that all three can read natively.
 *
 *  2. Per-environment splits (dev vs prod) are handled by the deployment
 *     pipeline (GitHub Actions injects the right secrets), not by multiple
 *     .env files on the developer's machine.
 *
 *  3. `.env.development` and similar files are .gitignored because they
 *     would still contain real secrets — so they offer no safety advantage
 *     over a plain `.env`.
 *
 * HOW .env.example RELATES TO .env
 * ──────────────────────────────────
 *  .env.example   → committed to git, blank/placeholder values, shows WHAT keys exist
 *  .env           → gitignored, your REAL values, never committed
 *
 *  Think of .env.example as the blank form and .env as your filled-in copy.
 *
 * HOW TO RUN
 * ──────────
 *  Automatically: runs as a pnpm postinstall hook (from src/StackAlchemist.Web/)
 *  Manually:      node scripts/setup-env.mjs   (from the solution root)
 *                 pnpm --filter stackalchemist-web run setup-env
 *
 * WHAT IT DOES
 * ────────────
 *  ✅ .env does NOT exist  → copies .env.example to .env and prints next steps
 *  ✅ .env ALREADY exists  → does nothing, prints a confirmation (idempotent)
 *  ✅ .env.example missing → prints a clear error (should never happen in a
 *                            healthy checkout)
 *
 * WHAT IT DOES NOT DO
 * ───────────────────
 *  ✗ It does NOT prompt for values — open .env in your editor and fill them in.
 *  ✗ It does NOT validate that the keys are non-empty.
 *  ✗ It does NOT push secrets anywhere.
 */

import { existsSync, copyFileSync, readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, resolve } from 'path';

// ── Resolve paths ─────────────────────────────────────────────────────────────
//
// We resolve relative to the location of THIS script file (scripts/), not
// relative to process.cwd(), so the script works correctly whether it is
// invoked from the solution root, from src/StackAlchemist.Web/ (pnpm hook),
// or from any other directory.

const SCRIPTS_DIR  = dirname(fileURLToPath(import.meta.url)); // …/scripts/
const SOLUTION_ROOT = resolve(SCRIPTS_DIR, '..');             // …/ (solution root)
const EXAMPLE_PATH  = resolve(SOLUTION_ROOT, '.env.example');
const TARGET_PATH   = resolve(SOLUTION_ROOT, '.env');

// ── ANSI colour helpers (no external deps) ────────────────────────────────────
const green  = (s) => `\x1b[32m${s}\x1b[0m`;
const yellow = (s) => `\x1b[33m${s}\x1b[0m`;
const red    = (s) => `\x1b[31m${s}\x1b[0m`;
const bold   = (s) => `\x1b[1m${s}\x1b[0m`;
const dim    = (s) => `\x1b[2m${s}\x1b[0m`;

// ── Guard: .env.example must exist ────────────────────────────────────────────
if (!existsSync(EXAMPLE_PATH)) {
  console.error(red(`\n✗ Could not find .env.example at:\n  ${EXAMPLE_PATH}`));
  console.error(red('  This file should always be committed. Is your checkout complete?\n'));
  // Exit with a non-zero code so CI/CD pipelines catch this.
  process.exit(1);
}

// ── Main logic ────────────────────────────────────────────────────────────────
if (existsSync(TARGET_PATH)) {
  // .env already exists — do nothing.
  // We intentionally never overwrite an existing .env because the developer
  // may have already filled in real secrets that would be lost.
  console.log(green('\n✔ .env already exists — nothing to do.\n'));
  console.log(dim(`  Location: ${TARGET_PATH}\n`));
} else {
  // .env does not exist → copy the example file.
  copyFileSync(EXAMPLE_PATH, TARGET_PATH);

  console.log(green('\n✔ Created .env from .env.example\n'));
  console.log(bold('  Next steps:'));
  console.log('');
  console.log('  1. Open the .env file at the solution root:');
  console.log(dim(`       ${TARGET_PATH}`));
  console.log('');
  console.log('  2. Fill in your real values for the keys marked with placeholder');
  console.log('     values (e.g. "sk-ant-...", "pk_test_...", "your-r2-access-key-id").');
  console.log('');
  console.log('  3. The keys you need to fill in for local development are:');

  // Parse .env.example and list keys that still have placeholder values so the
  // developer knows exactly what to edit without opening the file.
  const exampleLines = readFileSync(EXAMPLE_PATH, 'utf-8').split('\n');
  const placeholderPatterns = [
    /^your-/i,    // e.g. "your-cloudflare-account-id"
    /^\.\.\./,    // e.g. "sk-ant-..."  "pk_test_..."
    /^whsec_/i,   // Stripe webhook placeholders
    /^sk-ant-/i,
    /^sk_test_/i,
    /^pk_test_/i,
    /^your-local-/i,
  ];
  const isPlaceholder = (v) =>
    placeholderPatterns.some((p) => p.test(v.trim()));

  for (const line of exampleLines) {
    if (!line || line.startsWith('#')) continue;
    const eqIdx = line.indexOf('=');
    if (eqIdx === -1) continue;
    const key = line.slice(0, eqIdx).trim();
    const val = line.slice(eqIdx + 1).trim().replace(/^"|"$/g, ''); // strip quotes
    if (isPlaceholder(val)) {
      console.log(yellow(`       ${key}`));
    }
  }

  console.log('');
  console.log(dim('  Keys already set to sensible local defaults (Supabase local,'));
  console.log(dim('  localhost ports, etc.) do not need to be changed for basic dev.\n'));
}
