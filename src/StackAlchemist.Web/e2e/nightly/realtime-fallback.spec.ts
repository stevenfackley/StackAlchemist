import { expect, test } from "@playwright/test";

test.describe("Integration: Realtime fallback", () => {
  test("simple-mode build still completes when websockets are blocked (polling fallback)", async ({
    page,
  }) => {
    // Kill every Supabase Realtime connection attempt — the watcher must fall
    // back to polling getGeneration and still redirect on success.
    await page.route("**/realtime/v1/**", (route) => route.abort());

    await page.goto("/");

    await page.getByTestId("home-mode-simple-button").click();
    await page
      .getByTestId("home-prompt-input")
      .fill("Build a CRM with companies, contacts, and deals.");
    await page.getByTestId("home-synthesize-button").click();

    await expect(page.getByTestId("simple-phase-building")).toBeVisible();
    // Polling cadence is 30s, so allow a couple of cycles on top of build time.
    await expect(page).toHaveURL(/\/generate\//, { timeout: 120_000 });
  });
});
