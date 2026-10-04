import { expect, test } from "@playwright/test";
import { signInViaKeycloak } from "../helpers/keycloak";

test.describe("Integration: Simple Mode Runtime Path", () => {
  // /simple and /generate are gated: sign in through the realm first (lands on /dashboard).
  test.beforeEach(async ({ page }) => {
    await signInViaKeycloak(page);
  });

  test("non-demo run builds a Spark generation from the home prompt handoff", async ({ page }) => {
    await page.goto("/");

    await page.getByTestId("home-mode-simple-button").click();
    await page.getByTestId("home-prompt-input").fill("Build a CRM with companies, contacts, and deals.");
    await page.getByTestId("home-synthesize-button").click();

    // /simple auto-submits one Spark build and shows the friendly building phase.
    // With no browser Supabase client (Postgres mode), it hard-navigates to the
    // result as soon as the submit action returns (SimpleModePage:
    // `isDemoMode || !supabase`), so that phase lives only for two server-action
    // round trips and can be gone before this assertion runs. Accept either state.
    await expect
      .poll(
        async () =>
          /\/generate\//.test(page.url()) ||
          (await page.getByTestId("simple-phase-building").isVisible()),
        { timeout: 10_000, message: "neither the building phase nor the result page appeared" },
      )
      .toBe(true);
    await expect(page).toHaveURL(/\/generate\//, { timeout: 60_000 });
    // The URL alone only proves the submit succeeded. The free-tier panel renders
    // only once the generation row is `success`, i.e. the engine's deterministic
    // Spark preview finished; the /generate watcher polls every 3 s.
    await expect(page.getByTestId("generate-free-tier-panel")).toBeVisible({ timeout: 60_000 });
  });
});
