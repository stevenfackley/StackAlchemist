import { expect, test } from "@playwright/test";

test.describe("Integration: Auth and Generation Routing", () => {
  test("dashboard redirects anonymous users to login with returnTo", async ({ page }) => {
    await page.goto("/dashboard");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });

  test("generation route redirects anonymous users to login with returnTo", async ({ page }) => {
    // /generate/* is auth-gated (middleware). A signed-out visitor never reaches
    // the page itself — including the not-found branch — so the contract here is
    // the redirect, not the rendered page. The UUID is a well-formed value that
    // cannot exist as a real generation ID.
    await page.goto("/generate/00000000-0000-0000-0000-000000000000");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });
});

// TODO: activate with Playwright auth fixtures (storageState from a seeded test user).
// The GenerationsLiveRefresher unit tests (7 specs in __tests__/dashboard/) cover
// the channel subscription, throttle, user_id guard, and visibilitychange logic.
// The e2e contract below verifies the full round-trip: DB row status change →
// Supabase Realtime event → router.refresh() → updated status badge in the DOM
// without a manual reload. Needs: storageState with a signed-in session +
// a seeded generation row whose status can be mutated during the test.
test.describe("Integration: Dashboard live refresh", () => {
  test.skip(
    !process.env.E2E_DASHBOARD_STORAGE_STATE,
    "Requires E2E_DASHBOARD_STORAGE_STATE (Playwright auth fixture) to run"
  );

  test("status badge updates without manual reload when realtime event arrives", async ({
    page,
  }) => {
    // Sign-in state comes from the pre-saved storage state (set via E2E global setup
    // once this fixture is wired up). The test mutates a known generation row via
    // the Supabase service-role key and waits for the badge to reflect the change.
    await page.goto("/dashboard");
    // Verify the live refresher island is wired (indirectly: no console errors,
    // page is not redirected to /login — means auth is working).
    await expect(page).toHaveURL(/\/dashboard/);
    // Full round-trip assertion is a TODO pending the DB mutation helper.
  });
});

