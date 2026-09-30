import "server-only";
import { drizzle, type PostgresJsDatabase } from "drizzle-orm/postgres-js";
import postgres from "postgres";
import * as schema from "./schema";

export type Db = PostgresJsDatabase<typeof schema>;

type Pool = { sql: postgres.Sql; db: Db };

const g = globalThis as unknown as { __saPools?: Map<string, Pool> };

/**
 * Lazy postgres-js + Drizzle client, cached per URL so a test can pass
 * `TEST_DATABASE_URL` while `DATABASE_URL` is also set. The cache lives on
 * globalThis to survive Next dev HMR.
 *
 * `prepare: false` is mandatory on the Supavisor transaction pooler (:6543)
 * that DATABASE_URL points at in prod; `max` stays small because the pooler
 * multiplexes. TLS comes from `?sslmode=require` in the URL (postgres-js reads
 * it there); in production `ssl: "require"` is forced on top as belt and
 * braces. The key is added ONLY in production: postgres-js merges options with
 * `k in o ? o[k] : query[k]`, so an explicit `ssl: undefined` would override
 * the URL's sslmode and connect in plaintext.
 *
 * A malformed URL (typically an unencoded `#` or `@` in the password) makes
 * postgres-js throw an error that carries the whole DSN on `.input`; it is
 * swallowed here and replaced with a message that never includes the URL.
 */
export function getDb(url = process.env.DATABASE_URL): Db {
  if (!url) throw new Error("DATABASE_URL is not set");
  const pools = (g.__saPools ??= new Map<string, Pool>());
  const hit = pools.get(url);
  if (hit) return hit.db;

  let sql: postgres.Sql;
  try {
    sql = postgres(url, {
      prepare: false,
      max: process.env.NODE_ENV === "production" ? 10 : 4,
      connect_timeout: 15,
      idle_timeout: 30,
      ...(process.env.NODE_ENV === "production" ? { ssl: "require" as const } : {}),
    });
  } catch {
    throw new Error("DATABASE_URL is not a valid postgres URL (percent-encode the password)");
  }
  const db = drizzle(sql, { schema });
  pools.set(url, { sql, db });
  return db;
}

export async function closeDb(): Promise<void> {
  const pools = g.__saPools;
  if (!pools) return;
  const entries = [...pools.values()];
  pools.clear();
  await Promise.all(entries.map((e) => e.sql.end({ timeout: 5 })));
}
