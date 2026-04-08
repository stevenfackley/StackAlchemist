import { expect, test } from "@playwright/test";

test.describe("Smoke: Checkout Entry Contract", () => {
  test("pricing page exposes stable tier cards and prices", async ({ page }) => {
    await page.goto("/pricing");

    await expect(page.getByTestId("pricing-page")).toBeVisible();
    await expect(page.getByTestId("pricing-hero-title")).toBeVisible();
    await expect(page.getByTestId("pricing-tier-spark")).toBeVisible();
    await expect(page.getByTestId("pricing-tier-blueprint")).toBeVisible();
    await expect(page.getByTestId("pricing-tier-boilerplate")).toBeVisible();
    await expect(page.getByTestId("pricing-tier-infrastructure")).toBeVisible();
    await expect(page.getByTestId("pricing-price-blueprint")).toHaveText("$299");
    await expect(page.getByTestId("pricing-price-boilerplate")).toHaveText("$599");
    await expect(page.getByTestId("pricing-price-infrastructure")).toHaveText("$999");
  });

  test("advanced tier selection exposes checkout control for paid tiers", async ({ page }) => {
    const paidTiers = [1, 2, 3];

    for (const tier of paidTiers) {
      await page.goto(`/advanced?step=5&tier=${tier}`);
      await expect(page.getByTestId("advanced-step-5")).toBeVisible();
      await expect(page.getByTestId(`advanced-tier-option-${tier}`)).toBeVisible();
      await expect(page.getByTestId("advanced-checkout-button")).toBeVisible();
    }
  });
});

