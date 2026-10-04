/**
 * Shared test doubles for `src/lib/actions.ts` server-action tests.
 *
 * Not a test file itself (no `.test.ts` suffix) — vitest's include glob only
 * picks up `*.test.ts` / `*.spec.ts` files under `__tests__`, so this file is
 * skipped, matching the repo's `__tests__/mocks/*` convention for shared
 * fixtures.
 */
import { vi, type Mock } from "vitest";
import type { DataStore } from "@/lib/data/store";

/** A `DataStore` whose every method is a `vi.fn`, so a test can arrange results and assert calls. */
export type FakeStore = { [K in keyof DataStore]: Mock<DataStore[K]> };

/**
 * Builds a fake store. Defaults describe an empty store where every write
 * succeeds: no profile, nothing counted, no rows, no retry claimed. Each test
 * file mocks `@/lib/data` so `getDataStore()` returns one of these, which keeps
 * actions.ts under test without a database (the real store has its own
 * integration suite, `__tests__/data/drizzle-store.integration.test.ts`).
 */
export function makeStore(): FakeStore {
  return {
    getProfile: vi.fn(async () => null),
    upsertProfile: vi.fn(async () => {}),
    ensureProfile: vi.fn(async () => {}),
    countFreeGenerationsThisMonth: vi.fn(async () => 0),
    insertGeneration: vi.fn(async () => {
      throw new Error("insertGeneration was not arranged by this test");
    }),
    getGenerationForUser: vi.fn(async () => null),
    resetForRetry: vi.fn(async () => false),
    listMyGenerations: vi.fn(async () => ({ generations: [], total: 0 })),
    generationStats: vi.fn(async () => ({ total: 0, completed: 0, inProgress: 0 })),
  };
}

/** Minimal `fetch` Response stub. */
export function fakeResponse(
  body: unknown,
  init: { ok?: boolean; status?: number; jsonThrows?: boolean } = {}
): Response {
  const ok = init.ok ?? true;
  const status = init.status ?? (ok ? 200 : 500);
  return {
    ok,
    status,
    json: vi.fn(async () => {
      if (init.jsonThrows) throw new Error("invalid json");
      return body;
    }),
    text: vi.fn(async () => (typeof body === "string" ? body : JSON.stringify(body))),
  } as unknown as Response;
}
