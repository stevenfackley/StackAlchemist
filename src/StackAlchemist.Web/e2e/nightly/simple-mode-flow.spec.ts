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

    // /simple auto-submits one Spark build and shows the friendly building phase…
    // unless the engine's deterministic Spark preview has already completed: in
    // Postgres mode the watcher's first catch-up fetch runs at once (no Realtime
    // handshake), and a finished row hard-navigates before the phase is visible.
    // Accept either state; the redirect below is the real assertion.
    await expect
      .poll(
        async () =>
          /\/generate\//.test(page.url()) ||
          (await page.getByTestId("simple-phase-building").isVisible()),
        { timeout: 10_000, message: "neither the building phase nor the result page appeared" },
      )
      .toBe(true);
    // …then the watcher (polling in Postgres mode) hard-navigates to the result page.
    await expect(page).toHaveURL(/\/generate\//, { timeout: 60_000 });
  });
});
