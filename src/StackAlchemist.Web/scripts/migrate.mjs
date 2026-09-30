import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readMigrationFiles } from "drizzle-orm/migrator";
import postgres from "postgres";

// Use the session-mode URL (:5432 on qavren-db). That is qavren-db's convention,
// not a technical limit of the lock: DDL runs in session mode as the app role
// (qavren-db README, "Migrations/DDL: Session mode :5432"). An xact-scoped
// advisory lock inside one transaction would also work in transaction mode.
// Prod URLs MUST carry `?sslmode=require`: postgres-js reads sslmode out of the URL.
const urlVar = process.env.DATABASE_URL_MIGRATE !== undefined ? "DATABASE_URL_MIGRATE" : "DATABASE_URL";
const url = process.env[urlVar];
if (!url) {
  console.error("Set DATABASE_URL_MIGRATE (session URL) or DATABASE_URL");
  process.exit(1);
}

const SCHEMA = "stackalchemist";
const TABLE = "__drizzle_migrations";
const LOCK_KEY = 7364102; // arbitrary, fixed: every migrator run contends on it

// Drizzle's stock migrate() opens with CREATE SCHEMA IF NOT EXISTS, which raises
// 42501 for the qavren-db role (no CREATE on the database), so we drive the same
// loop ourselves: one transaction, advisory lock first, ledger inside the app
// schema, statements split on `--> statement-breakpoint` (not `;`, so plpgsql
// bodies survive). See recharacter web/scripts/migrate.ts for the full history.
const here = path.dirname(fileURLToPath(import.meta.url));
const migrationsFolder = path.resolve(here, "../drizzle");
const migrations = readMigrationFiles({
  migrationsFolder,
  migrationsSchema: SCHEMA,
  migrationsTable: TABLE,
});
// readMigrationFiles drops the journal tag; pair on the timestamp (folderMillis is
// the journal's `when`), not on array position.
const journal = JSON.parse(
  fs.readFileSync(path.join(migrationsFolder, "meta", "_journal.json"), "utf8"),
);
const tagByMillis = new Map(journal.entries.map((entry) => [entry.when, entry.tag]));

let sql;
let current; // tag of the migration being applied, for the failure log
let applied = 0;
try {
  // Built inside the try: a malformed DSN (unencoded `#` or `@` in the password)
  // makes Node throw ERR_INVALID_URL with the whole URL on `.input`.
  sql = postgres(url, { max: 1, prepare: false, onnotice: () => {} });

  await sql.begin(async (tx) => {
    // The lock wait is bounded by lock_timeout: generous while queueing behind
    // another migrator, then tight for the DDL itself.
    await tx.unsafe(`SET LOCAL lock_timeout = '2min'`);
    await tx.unsafe(`select pg_advisory_xact_lock(${LOCK_KEY})`);
    await tx.unsafe(`SET LOCAL lock_timeout = '5s'`);
    await tx.unsafe(`SET LOCAL statement_timeout = '5min'`);

    const [existing] = await tx`select 1 from pg_namespace where nspname = ${SCHEMA}`;
    if (!existing) await tx.unsafe(`CREATE SCHEMA "${SCHEMA}"`);

    await tx.unsafe(
      `CREATE TABLE IF NOT EXISTS "${SCHEMA}"."${TABLE}" (id SERIAL PRIMARY KEY, hash text NOT NULL, created_at bigint)`,
    );
    const ledger = await tx.unsafe(
      `select hash, created_at from "${SCHEMA}"."${TABLE}" order by created_at`,
    );
    const appliedHashes = new Map(ledger.map((row) => [Number(row.created_at), row.hash]));
    const maxApplied = ledger.reduce((max, row) => Math.max(max, Number(row.created_at)), -Infinity);

    for (const migration of migrations) {
      const tag = tagByMillis.get(migration.folderMillis);
      current = tag;
      const storedHash = appliedHashes.get(migration.folderMillis);
      if (storedHash !== undefined) {
        if (storedHash !== migration.hash) {
          throw new Error(
            `Migration ${tag} was edited after it was applied (hash mismatch); never edit an applied migration`,
          );
        }
        continue;
      }
      if (migration.folderMillis < maxApplied) {
        throw new Error(`Migration ${tag} is older than the newest applied migration; renumber it`);
      }
      for (const stmt of migration.sql) await tx.unsafe(stmt);
      await tx.unsafe(
        `insert into "${SCHEMA}"."${TABLE}" ("hash", "created_at") values($1, $2)`,
        [migration.hash, migration.folderMillis],
      );
      applied += 1;
    }
  });
  console.log(applied === 0 ? "no pending migrations (ledger up to date)" : `migrations applied (${applied})`);
} catch (err) {
  if (!sql) {
    console.error({ message: `${urlVar} is not a valid postgres URL (percent-encode the password)` });
  } else {
    // Named fields ONLY, never the error object: none of these carry the DSN.
    console.error({
      migration: current,
      code: err?.code,
      message: err?.message,
      detail: err?.detail,
      hint: err?.hint,
      position: err?.position,
    });
  }
  process.exitCode = 1;
} finally {
  await sql?.end();
}
