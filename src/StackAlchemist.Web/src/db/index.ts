import "server-only";
import { drizzle, type PostgresJsDatabase } from "drizzle-orm/postgres-js";
import postgres from "postgres";
import * as schema from "./schema";

export type Db = PostgresJsDatabase<typeof schema>;

const g = globalThis as unknown as { __saSql?: postgres.Sql; __saDb?: Db };

/**
 * `prepare: false` is mandatory on the Supavisor transaction pooler (:6543)
 * that DATABASE_URL points at in prod; `max` stays small because the pooler
 * multiplexes. Survives Next dev HMR via globalThis. DATABASE_URL carries
 * `?sslmode=require` in prod (postgres-js reads it from the URL); the explicit
 * `ssl` below is belt and braces for a URL that lost the parameter.
 */
export function getDb(url = process.env.DATABASE_URL): Db {
  if (!url) throw new Error("DATABASE_URL is not set");
  if (g.__saDb) return g.__saDb;
  const sql = postgres(url, {
    prepare: false,
    max: process.env.NODE_ENV === "production" ? 10 : 4,
    connect_timeout: 15,
    idle_timeout: 30,
    ssl: process.env.NODE_ENV === "production" ? "require" : undefined,
  });
  g.__saSql = sql;
  g.__saDb = drizzle(sql, { schema });
  return g.__saDb;
}

export async function closeDb(): Promise<void> {
  await g.__saSql?.end({ timeout: 5 });
  g.__saSql = undefined;
  g.__saDb = undefined;
}
