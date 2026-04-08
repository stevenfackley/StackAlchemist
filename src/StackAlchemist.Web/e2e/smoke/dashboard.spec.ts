import { expect, test } from "@playwright/test";

test.describe("Smoke: Auth and Demo Generation Contract", () => {
  test("dashboard redirects anonymous users to login with returnTo", async ({ page }) => {
    await page.goto("/dashboard");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });

  test("login and register entry points render", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByRole("heading", { name: /sign in/i })).toBeVisible();
    await expect(page.getByRole("button", { name: /sign in|send magic link/i })).toBeVisible();

    await page.goto("/register");
    await expect(page.getByRole("heading", { name: /create account/i })).toBeVisible();
    await expect(page.getByRole("button", { name: /create account/i })).toBeVisible();
  });

  test("unknown generation follows demo-mode smoke contract", async ({ page }) => {
    await page.goto("/generate/does-not-exist");

    await expect(page.getByTestId("generate-page")).toBeVisible();
    await expect(page.getByTestId("generate-free-tier-panel")).toBeVisible();
  });
});

