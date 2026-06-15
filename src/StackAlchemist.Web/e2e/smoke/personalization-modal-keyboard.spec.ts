import { expect, test } from "@playwright/test";

test.describe("Smoke: Personalization modal keyboard a11y", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/advanced?step=4");
    await expect(page.getByTestId("advanced-step-4")).toBeVisible();
  });

  test("modal opens, has role=dialog, and closes on Escape", async ({ page }) => {
    await page.getByTestId("advanced-personalize-button").click();
    const dialog = page.getByRole("dialog", { name: /make it yours/i });
    await expect(dialog).toBeVisible();

    await page.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();
  });

  test("Tab stays trapped inside the modal while it is open", async ({ page }) => {
    await page.getByTestId("advanced-personalize-button").click();
    await expect(page.getByRole("dialog")).toBeVisible();

    // Tab through all focusable elements — focus must not escape to the page behind.
    for (let i = 0; i < 10; i++) {
      await page.keyboard.press("Tab");
      const focused = page.locator(":focus");
      const dialogHandle = page.getByRole("dialog");
      await expect(dialogHandle).toContainText(await focused.innerText().catch(() => ""));
    }

    // Confirm: after Escape, some element on the page (opener button) receives focus.
    await page.keyboard.press("Escape");
    await expect(page.getByTestId("advanced-personalize-button")).toBeFocused();
  });
});
