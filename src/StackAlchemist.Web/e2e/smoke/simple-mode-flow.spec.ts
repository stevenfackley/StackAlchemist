import { expect, test, type Page } from "@playwright/test";

async function waitForSimpleCanvasOrTier(page: Page) {
  await expect
    .poll(
      async () => {
        if (await page.getByTestId("simple-phase-tier").isVisible()) return "tier";
        if (await page.getByTestId("simple-phase-canvas").isVisible()) return "canvas";
        return "pending";
      },
      { timeout: 45_000, message: "Simple mode should progress to canvas or tier selection." }
    )
    .toMatch(/canvas|tier/);
}

test.describe("Smoke: Simple Mode Contract", () => {
  test("home prompt submission routes to /simple?q and reaches tier selection", async ({ page }) => {
    await page.goto("/");

    await expect(page.getByTestId("home-hero-title")).toBeVisible();
    await expect(page.getByTestId("home-launch-console")).toBeVisible();
    await expect(page.getByTestId("home-mode-simple-button")).toBeVisible();

    const prompt = "Build a task tracking app with users, projects, and subscriptions";
    await page.getByTestId("home-prompt-input").fill(prompt);
    await page.getByTestId("home-synthesize-button").click();

    await expect(page).toHaveURL(/\/simple\?q=/);
    await expect(page.getByTestId("simple-prompt-value")).toContainText("task tracking app");
    await expect(page.getByTestId("simple-phase-generating")).toBeVisible();

    await waitForSimpleCanvasOrTier(page);

    if (await page.getByTestId("simple-phase-canvas").isVisible()) {
      await page.getByTestId("simple-confirm-tier-button").click();
    }

    await expect(page.getByTestId("simple-phase-tier")).toBeVisible();
    await expect(page.getByTestId("simple-tier-option-0")).toBeVisible();
    await expect(page.getByTestId("simple-tier-option-1")).toBeVisible();
    await expect(page.getByTestId("simple-tier-option-2")).toBeVisible();
    await expect(page.getByTestId("simple-tier-option-3")).toBeVisible();
    await expect(page.getByTestId("simple-personalize-continue")).toBeVisible();
  });

  test("missing q on /simple keeps current behavior and does not redirect", async ({ page }) => {
    await page.goto("/simple");

    await expect(page).toHaveURL(/\/simple$/);
    await expect(page.getByTestId("simple-prompt-value")).toHaveText("No prompt provided.");
    await expect(page.getByTestId("simple-phase-generating")).toBeVisible();
    await expect(page.getByTestId("simple-phase-canvas")).toHaveCount(0);
    await expect(page.getByTestId("simple-confirm-tier-button")).toHaveCount(0);
  });
});
