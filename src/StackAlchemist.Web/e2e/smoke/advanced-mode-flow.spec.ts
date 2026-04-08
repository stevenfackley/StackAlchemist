import { expect, test } from "@playwright/test";

test.describe("Smoke: Advanced Mode Contract", () => {
  test("wizard loads with stable stepper contract", async ({ page }) => {
    await page.goto("/advanced");

    await expect(page).toHaveURL(/\/advanced/);
    await expect(page.getByTestId("advanced-mode-page")).toBeVisible();
    await expect(page.getByTestId("advanced-stepper")).toBeVisible();
    await expect(page.getByTestId("advanced-stepper-button-1")).toBeVisible();
    await expect(page.getByTestId("advanced-step-1")).toBeVisible();
  });

  test("step navigation stays scoped to step containers", async ({ page }) => {
    await page.goto("/advanced?step=3");

    await expect(page.getByTestId("advanced-step-3")).toBeVisible();
    await page.getByTestId("advanced-next-button").click();
    await expect(page.getByTestId("advanced-step-4")).toBeVisible();
    await page.getByTestId("advanced-previous-button").click();
    await expect(page.getByTestId("advanced-step-3")).toBeVisible();
  });

  test("step 4 personalization assertion uses explicit CTA selector", async ({ page }) => {
    await page.goto("/advanced?step=4");

    await expect(page.getByTestId("advanced-step-4")).toBeVisible();
    await expect(page.getByTestId("advanced-personalization-section")).toBeVisible();
    await expect(page.getByTestId("advanced-personalize-button")).toBeVisible();
  });

  test("step 5 exposes stable tier and checkout controls", async ({ page }) => {
    await page.goto("/advanced?step=5&tier=2");

    await expect(page.getByTestId("advanced-step-5")).toBeVisible();
    await expect(page.getByTestId("advanced-tier-option-0")).toBeVisible();
    await expect(page.getByTestId("advanced-tier-option-1")).toBeVisible();
    await expect(page.getByTestId("advanced-tier-option-2")).toBeVisible();
    await expect(page.getByTestId("advanced-tier-option-3")).toBeVisible();
    await expect(page.getByTestId("advanced-checkout-button")).toBeVisible();
  });
});

