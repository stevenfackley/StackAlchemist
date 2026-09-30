import path from "node:path";
import { fileURLToPath } from "node:url";
import { readMigrationFiles } from "drizzle-orm/migrator";
import postgres from "postgres";

// Session-mode URL (:5432 on qavren-db). The transaction pooler cannot hold
// the advisory lock or the long transaction this needs. Prod URLs MUST carry
// `?sslmode=require`: postgres-js reads sslmode out of the URL.
const url = process.env.DATABASE_URL_MIGRATE ?? process.env.DATABASE_URL;
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
const migrations = readMigrationFiles({
  migrationsFolder: path.resolve(here, "../drizzle"),
  migrationsSchema: SCHEMA,
  migrationsTable: TABLE,
});

const sql = postgres(url, { max: 1, prepare: false, onnotice: () => {} });
let applied = 0;
try {
  await sql.begin(async (tx) => {
    await tx.unsafe(`select pg_advisory_xact_lock(${LOCK_KEY})`);
    await tx.unsafe(`SET LOCAL lock_timeout = '5s'`);
    await tx.unsafe(`SET LOCAL statement_timeout = '5min'`);

    const [existing] = await tx`select 1 from pg_namespace where nspname = ${SCHEMA}`;
    if (!existing) await tx.unsafe(`CREATE SCHEMA "${SCHEMA}"`);

    await tx.unsafe(
      `CREATE TABLE IF NOT EXISTS "${SCHEMA}"."${TABLE}" (id SERIAL PRIMARY KEY, hash text NOT NULL, created_at bigint)`,
    );
    const [last] = await tx.unsafe(
      `select created_at from "${SCHEMA}"."${TABLE}" order by created_at desc limit 1`,
    );
    for (const migration of migrations) {
      if (last && Number(last.created_at) >= migration.folderMillis) continue;
      for (const stmt of migration.sql) await tx.unsafe(stmt);
      await tx.unsafe(
        `insert into "${SCHEMA}"."${TABLE}" ("hash", "created_at") values($1, $2)`,
        [migration.hash, migration.folderMillis],
      );
      applied += 1;
    }
  });
  console.log(applied === 0 ? "migrations applied (already up to date)" : `migrations applied (${applied})`);
} catch (err) {
  // Code and message ONLY: a postgres-js connection error carries the full DSN
  // (password included) on err.input.
  console.error({ code: err?.code, message: err?.message });
  process.exitCode = 1;
} finally {
  await sql.end();
}
