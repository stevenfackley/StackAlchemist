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

    // Tab through focusable elements — focus must stay trapped inside the dialog,
    // not escape to the page behind. Assert containment directly: comparing the
    // focused element's innerText (CSS-uppercased) against the dialog's textContent
    // (raw, mixed-case) was brittle and never matched — the original bug.
    for (let i = 0; i < 10; i++) {
      await page.keyboard.press("Tab");
      const trapped = await page.evaluate(() => {
        const dialog = document.querySelector('[role="dialog"]');
        return !!dialog && dialog.contains(document.activeElement);
      });
      expect(trapped, `focus escaped the dialog after Tab #${i + 1}`).toBe(true);
    }

    // Confirm: after Escape, some element on the page (opener button) receives focus.
    await page.keyboard.press("Escape");
    await expect(page.getByTestId("advanced-personalize-button")).toBeFocused();
  });
});
