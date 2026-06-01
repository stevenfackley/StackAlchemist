import { expect, test } from "@playwright/test";

test.describe("Integration: Auth and Generation Routing", () => {
  test("dashboard redirects anonymous users to login with returnTo", async ({ page }) => {
    await page.goto("/dashboard");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });

  test("generation route redirects anonymous users to login with returnTo", async ({ page }) => {
    // /generate/* is auth-gated (middleware). A signed-out visitor never reaches
    // the page itself — including the not-found branch — so the contract here is
    // the redirect, not the rendered page. The UUID is a well-formed value that
    // cannot exist as a real generation ID.
    await page.goto("/generate/00000000-0000-0000-0000-000000000000");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });
});

