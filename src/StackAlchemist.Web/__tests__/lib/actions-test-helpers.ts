/**
 * Shared test doubles for `src/lib/actions.ts` server-action tests.
 *
 * Not a test file itself (no `.test.ts` suffix) — vitest's include glob only
 * picks up `*.test.ts` / `*.spec.ts` files under `__tests__`, so this file is
 * skipped, matching the repo's `__tests__/mocks/*` convention for shared
 * fixtures.
 */
import { vi } from "vitest";

/**
 * Builds a chainable, "thenable" Supabase query-builder stub. Every builder
 * method (`.select()`, `.eq()`, `.insert()`, ...) returns the same object so
 * calls can be chained in any order/length, and awaiting the chain at any
 * point resolves to `result` — mirroring how the real `@supabase/supabase-js`
 * query builder is itself a thenable.
 */
export function chainable<T>(result: T): Record<string, unknown> {
  const handler: Record<string, unknown> = {};
  const methods = [
    "select",
    "insert",
    "update",
    "upsert",
    "delete",
    "eq",
    "neq",
    "gte",
    "lte",
    "gt",
    "lt",
    "order",
    "limit",
    "single",
    "maybeSingle",
  ];
  for (const name of methods) {
    handler[name] = vi.fn(() => handler);
  }
  handler.then = (resolve: (value: T) => unknown, reject?: (reason: unknown) => unknown) =>
    Promise.resolve(result).then(resolve, reject);
  handler.catch = (reject: (reason: unknown) => unknown) => Promise.resolve(result).catch(reject);
  return handler;
}

/**
 * Builds a fake Supabase client whose `.from(table)` yields each entry of
 * `results`, in order, one per call — matching the fact that `actions.ts`
 * calls `.from(...)` once per logical query, even against the same table.
 */
export function makeDb(results: unknown[]): { from: ReturnType<typeof vi.fn> } {
  const from = vi.fn();
  for (const result of results) {
    from.mockReturnValueOnce(chainable(result));
  }
  return { from };
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
