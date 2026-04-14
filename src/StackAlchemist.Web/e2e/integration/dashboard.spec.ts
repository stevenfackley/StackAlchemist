import { expect, test } from "@playwright/test";

test.describe("Integration: Auth and Generation Routing", () => {
  test("dashboard redirects anonymous users to login with returnTo", async ({ page }) => {
    await page.goto("/dashboard");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });

  test("unknown generation renders not-found state in non-demo mode", async ({ page }) => {
    // Use a UUID that cannot exist as a real generation ID.
    // "does-not-exist" was a literal string that ended up as a real DB row,
    // causing this test to always fail on the test environment.
    await page.goto("/generate/00000000-0000-0000-0000-000000000000");

    await expect(page.getByTestId("generate-not-found-heading")).toBeVisible();
    await expect(page.getByTestId("generate-not-found-start-link")).toBeVisible();
  });
});

