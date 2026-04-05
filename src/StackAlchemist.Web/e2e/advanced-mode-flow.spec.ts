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
    await expect(page.getByText(/platform selection/i).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /\.net 10 \+ next\.js 15/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /fastapi \+ react\/vite/i })).toBeVisible();
  });

  test('should complete Step 3: Configure API Endpoints', async ({ page }) => {
    await page.goto('/advanced?step=3');
    await expect(page.getByText(/define api endpoints/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /add endpoint/i })).toBeVisible();
  });

  test('should complete Step 4: Select Tier and pay', async ({ page }) => {
    await page.goto('/advanced?step=4');
    await expect(page.getByText(/select tier/i).first()).toBeVisible();
    await expect(page.getByText(/spark/i).first()).toBeVisible();
    await expect(page.getByText(/boilerplate/i).first()).toBeVisible();
  });

  test('should allow navigating back between steps', async ({ page }) => {
    await page.goto('/advanced?step=3');
    const previousButton = page.getByRole('button', { name: /previous/i });
    await expect(previousButton).toBeVisible();
    await previousButton.click();
    await expect(page.getByText(/platform selection/i).first()).toBeVisible();
  });

  test('should validate required fields before advancing', async ({ page }) => {
    await page.goto('/advanced?step=1');
    await page.getByRole('button', { name: /next/i }).click();
    await expect(page.getByText(/platform selection/i).first()).toBeVisible();
  });
});
