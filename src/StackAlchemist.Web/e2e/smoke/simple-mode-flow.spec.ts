import { expect, test } from "@playwright/test";

test.describe("Smoke: Simple Mode Contract", () => {
  test("home prompt submission builds a Spark generation and lands on the live preview", async ({ page }) => {
    await page.goto("/");

    await expect(page.getByTestId("home-hero-title")).toBeVisible();
    await expect(page.getByTestId("home-launch-console")).toBeVisible();
    await expect(page.getByTestId("home-mode-simple-button")).toBeVisible();

    const prompt = "Build a task tracking app with users, projects, and subscriptions";
    await page.getByTestId("home-prompt-input").fill(prompt);
    await page.getByTestId("home-synthesize-button").click();

    // Streamlined flow: the prompt handoff lands on /simple, which auto-submits a
    // single Spark (tier 0) build — no schema canvas, no up-front tier picker —
    // then hard-navigates to the result page once the preview is ready.
    await expect(page).toHaveURL(/\/generate\//, { timeout: 45_000 });
    await expect(page.getByTestId("generate-page")).toBeVisible();
  });

  test("missing q on /simple shows an error and does not build", async ({ page }) => {
    await page.goto("/simple");

    await expect(page).toHaveURL(/\/simple$/);
    await expect(page.getByTestId("simple-prompt-value")).toHaveText("No prompt provided.");
    await expect(page.getByTestId("simple-phase-error")).toBeVisible();
  });
});
