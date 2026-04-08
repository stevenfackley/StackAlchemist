import { expect, test, type Page } from "@playwright/test";

async function waitForSimpleCanvasOrTier(page: Page) {
  await expect
    .poll(
      async () => {
        if (await page.getByTestId("simple-phase-tier").isVisible()) return "tier";
        if (await page.getByTestId("simple-phase-canvas").isVisible()) return "canvas";
        return "pending";
      },
      { timeout: 60_000, message: "Simple mode should reach canvas or tier in integration runs." }
    )
    .toMatch(/canvas|tier/);
}

test.describe("Integration: Simple Mode Runtime Path", () => {
  test("non-demo run still supports home prompt handoff to simple route", async ({ page }) => {
    await page.goto("/");

    await page.getByTestId("home-mode-simple-button").click();
    await page.getByTestId("home-prompt-input").fill("Build a CRM with companies, contacts, and deals.");
    await page.getByTestId("home-synthesize-button").click();

    await expect(page).toHaveURL(/\/simple\?q=/);
    await expect(page.getByTestId("simple-phase-generating")).toBeVisible();
    await waitForSimpleCanvasOrTier(page);
  });
});

