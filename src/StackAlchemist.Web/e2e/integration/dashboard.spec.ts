import { expect, test } from "@playwright/test";

test.describe("Integration: Auth and Generation Routing", () => {
  test("dashboard redirects anonymous users to login with returnTo", async ({ page }) => {
    await page.goto("/dashboard");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });

  test("unknown generation renders not-found state in non-demo mode", async ({ page }) => {
    await page.goto("/generate/does-not-exist");

    await expect(page.getByTestId("generate-not-found-heading")).toBeVisible();
    await expect(page.getByTestId("generate-not-found-start-link")).toBeVisible();
  });
});

