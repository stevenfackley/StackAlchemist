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
    // Kill every Supabase Realtime connection attempt — in Supabase mode the
    // watcher must fall back to polling getGeneration and still reach success.
    // In Postgres mode (this lane) there is no browser Supabase client, so this
    // route matches nothing and the spec is a second Spark end-to-end run; it
    // stays until phase F retires Realtime.
    await page.route("**/realtime/v1/**", (route) => route.abort());

    await page.goto("/");

    await page.getByTestId("home-mode-simple-button").click();
    await page
      .getByTestId("home-prompt-input")
      .fill("Build a CRM with companies, contacts, and deals.");
    await page.getByTestId("home-synthesize-button").click();

    // With no browser Supabase client, /simple hard-navigates to the result as
    // soon as the submit action returns (SimpleModePage: `isDemoMode || !supabase`),
    // so the building phase lives only for two server-action round trips and
    // can be gone before this assertion runs. Accept either state.
    await expect
      .poll(
        async () =>
          /\/generate\//.test(page.url()) ||
          (await page.getByTestId("simple-phase-building").isVisible()),
        { timeout: 10_000, message: "neither the building phase nor the result page appeared" },
      )
      .toBe(true);
    await expect(page).toHaveURL(/\/generate\//, { timeout: 120_000 });
    // The URL alone only proves the submit succeeded. The free-tier panel renders
    // only once the generation row is `success`, i.e. the Spark build finished;
    // the /generate watcher polls every 30 s without Realtime, so allow a cycle.
    await expect(page.getByTestId("generate-free-tier-panel")).toBeVisible({ timeout: 60_000 });
  });
});
