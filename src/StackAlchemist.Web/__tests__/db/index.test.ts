// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
import { closeDb, getDb } from "@/db";

// postgres-js connects lazily, so none of these tests need a database.
const URL_A = "postgres://app:secret@127.0.0.1:1/a";
const URL_B = "postgres://app:secret@127.0.0.1:1/b";

afterEach(async () => {
  await closeDb();
  vi.unstubAllEnvs();
});

describe("getDb", () => {
  it("throws when no URL is passed and DATABASE_URL is unset", () => {
    vi.stubEnv("DATABASE_URL", undefined);
    expect(() => getDb()).toThrow(/DATABASE_URL is not set/);
  });

  it("rejects a malformed URL without leaking the DSN", () => {
    let thrown: unknown;
    try {
      getDb("postgres://app:s3cr#et@db.example:5432/x");
    } catch (err) {
      thrown = err;
    }
    expect(thrown).toBeInstanceOf(Error);
    const err = thrown as Error & { input?: unknown; cause?: unknown };
    expect(err.message).toMatch(/not a valid postgres URL/);
    expect(err.message).not.toContain("s3cr");
    expect(err.input).toBeUndefined();
    expect(err.cause).toBeUndefined();
  });

  it("caches one client per URL", () => {
    const a = getDb(URL_A);
    const b = getDb(URL_B);
    expect(a).not.toBe(b);
    expect(getDb(URL_A)).toBe(a);
    expect(getDb(URL_B)).toBe(b);
  });

  it("falls back to DATABASE_URL and shares that client with the same explicit URL", () => {
    vi.stubEnv("DATABASE_URL", URL_A);
    expect(getDb()).toBe(getDb(URL_A));
  });

  it("keeps sslmode from the URL outside production (no explicit ssl: undefined)", () => {
    vi.stubEnv("NODE_ENV", "test");
    const db = getDb(`${URL_A}?sslmode=require`);
    expect(db.$client.options.ssl).toBe("require");
  });

  it("forces ssl: require in production even when the URL has no sslmode", () => {
    vi.stubEnv("NODE_ENV", "production");
    const db = getDb(URL_A);
    expect(db.$client.options.ssl).toBe("require");
  });

  it("closeDb clears the cache so the next call builds a fresh client", async () => {
    const first = getDb(URL_A);
    await closeDb();
    expect(getDb(URL_A)).not.toBe(first);
  });
});
