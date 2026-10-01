import { expect, test } from "@playwright/test";
import { signInViaKeycloak } from "../helpers/keycloak";

test.describe("Integration: Realtime fallback", () => {
  // /simple and /generate are gated: sign in through the realm first (lands on /dashboard).
  test.beforeEach(async ({ page }) => {
    await signInViaKeycloak(page);
  });

  test("simple-mode build still completes when websockets are blocked (polling fallback)", async ({
    page,
  }) => {
    // Kill every Supabase Realtime connection attempt — the watcher must fall
    // back to polling getGeneration and still redirect on success.
    // In Postgres mode polling is the only path (kept until phase F retires Realtime).
    await page.route("**/realtime/v1/**", (route) => route.abort());

    await page.goto("/");

    await page.getByTestId("home-mode-simple-button").click();
    await page
      .getByTestId("home-prompt-input")
      .fill("Build a CRM with companies, contacts, and deals.");
    await page.getByTestId("home-synthesize-button").click();

    // The building phase is visible only while the build is in flight. A Tier 0
    // preview completes in milliseconds, and in Postgres mode there is no
    // Realtime handshake to delay the watcher's first catch-up fetch, so /simple
    // can hard-navigate to the result before this assertion runs. Accept either
    // state; the redirect below is the real assertion.
    await expect
      .poll(
        async () =>
          /\/generate\//.test(page.url()) ||
          (await page.getByTestId("simple-phase-building").isVisible()),
        { timeout: 10_000, message: "neither the building phase nor the result page appeared" },
      )
      .toBe(true);
    // Polling cadence is 30s, so allow a couple of cycles on top of build time.
    await expect(page).toHaveURL(/\/generate\//, { timeout: 120_000 });
  });
});
