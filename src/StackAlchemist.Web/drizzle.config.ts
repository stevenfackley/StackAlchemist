import { defineConfig } from "drizzle-kit";

/**
 * `drizzle-kit generate` ONLY. Never run `drizzle-kit migrate` or `push`
 * against qavren-db: both open with `CREATE SCHEMA IF NOT EXISTS`, which
 * Postgres refuses with 42501 for a role that lacks CREATE on the database —
 * which the app role does, even though it owns the `stackalchemist` schema.
 * Migrations are applied by `npm run db:migrate` (scripts/migrate.mjs).
 */
export default defineConfig({
  dialect: "postgresql",
  schema: "./src/db/schema.ts",
  out: "./drizzle",
  schemaFilter: ["stackalchemist"],
  migrations: { schema: "stackalchemist", table: "__drizzle_migrations" },
  dbCredentials: { url: process.env.DATABASE_URL_MIGRATE ?? process.env.DATABASE_URL ?? "" },
});
