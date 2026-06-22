import { expect, test } from "@playwright/test";

test.describe("Smoke: Advanced Draft Persistence", () => {
  test.beforeEach(async ({ page }) => {
    // Drafts from prior specs/sessions must not leak in. Clear storage ONCE —
    // NOT via page.addInitScript(), which re-runs on every navigation including
    // the page.reload() these tests depend on, wiping the draft mid-test.
    await page.goto("/advanced");
    await page.evaluate(() => window.localStorage.clear());
  });

  test("wizard edits survive a reload and show the restore notice", async ({ page }) => {
    await page.goto("/advanced?step=1");

    const entityName = page.getByPlaceholder("EntityName");
    await expect(entityName).toHaveValue("Product");
    await entityName.fill("Subscription");
    // Outlive the 800ms persist debounce before reloading.
    await page.waitForTimeout(1200);

    await page.reload();

    await expect(page.getByPlaceholder("EntityName")).toHaveValue("Subscription");
    await expect(page.getByTestId("advanced-draft-restored")).toBeVisible();
  });

  test("start fresh discards the draft", async ({ page }) => {
    await page.goto("/advanced?step=1");
    await page.getByPlaceholder("EntityName").fill("Subscription");
    await page.waitForTimeout(1200);
    await page.reload();
    await expect(page.getByTestId("advanced-draft-restored")).toBeVisible();

    await page.getByRole("button", { name: "start fresh" }).click();

    await expect(page.getByPlaceholder("EntityName")).toHaveValue("Product");
    await page.reload();
    await expect(page.getByTestId("advanced-draft-restored")).not.toBeVisible();
  });

  test("step navigation is reflected in the URL for refresh-safety", async ({ page }) => {
    await page.goto("/advanced");
    await expect(page).toHaveURL(/step=1/);
  });
});
