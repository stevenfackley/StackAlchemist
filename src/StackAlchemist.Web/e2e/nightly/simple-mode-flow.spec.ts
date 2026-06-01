import { expect, test } from "@playwright/test";

test.describe("Integration: Simple Mode Runtime Path", () => {
  test("non-demo run builds a Spark generation from the home prompt handoff", async ({ page }) => {
    await page.goto("/");

    await page.getByTestId("home-mode-simple-button").click();
    await page.getByTestId("home-prompt-input").fill("Build a CRM with companies, contacts, and deals.");
    await page.getByTestId("home-synthesize-button").click();

    // /simple auto-submits one Spark build and shows the friendly building phase…
    await expect(page.getByTestId("simple-phase-building")).toBeVisible();
    // …then the engine's deterministic Spark preview completes and realtime
    // hard-navigates to the result page.
    await expect(page).toHaveURL(/\/generate\//, { timeout: 60_000 });
  });
});
