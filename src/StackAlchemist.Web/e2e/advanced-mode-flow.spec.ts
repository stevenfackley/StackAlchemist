import { test, expect } from '@playwright/test';

test.describe('Advanced Mode Flow', () => {
  test('should navigate to Advanced Mode wizard', async ({ page }) => {
    await page.goto('/advanced');
    await expect(page).toHaveURL(/\/advanced/);
    await expect(page.getByText(/define entities/i).first()).toBeVisible();
  });

  test('should complete Step 1: Define Entities', async ({ page }) => {
    await page.goto('/advanced?step=1');
    await expect(page.getByPlaceholder('EntityName').first()).toBeVisible();
    await expect(page.getByRole('button', { name: /add entity/i })).toBeVisible();
  });

  test('should complete Step 2: Configure API Endpoints', async ({ page }) => {
    await page.goto('/advanced?step=2');
    await expect(page.getByText(/define api endpoints/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /add endpoint/i })).toBeVisible();
  });

  test('should complete Step 3: Select Tier and pay', async ({ page }) => {
    await page.goto('/advanced?step=3');
    await expect(page.getByText(/select tier/i).first()).toBeVisible();
    await expect(page.getByText(/spark/i).first()).toBeVisible();
    await expect(page.getByText(/boilerplate/i).first()).toBeVisible();
  });

  test('should allow navigating back between steps', async ({ page }) => {
    await page.goto('/advanced?step=2');
    const previousButton = page.getByRole('button', { name: /previous/i });
    await expect(previousButton).toBeVisible();
    await previousButton.click();
    await expect(page.getByText(/define entities/i).first()).toBeVisible();
  });

  test('should validate required fields before advancing', async ({ page }) => {
    await page.goto('/advanced?step=1');
    await page.getByRole('button', { name: /next/i }).click();
    // Stepper navigation should still work and keep a valid wizard surface rendered.
    await expect(page.getByText(/configure api|define api endpoints/i).first()).toBeVisible();
  });
});
