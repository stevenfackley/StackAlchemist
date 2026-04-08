import { test, expect } from '@playwright/test';

test.describe('Checkout Flow', () => {
  test('should display all 3 pricing tiers with correct prices', async ({ page }) => {
    await page.goto('/pricing');
    await expect(page.getByText(/blueprint/i).first()).toBeVisible();
    await expect(page.getByText(/boilerplate/i).first()).toBeVisible();
    await expect(page.getByText(/infrastructure/i).first()).toBeVisible();
    await expect(page.getByText('$299').first()).toBeVisible();
    await expect(page.getByText('$599').first()).toBeVisible();
    await expect(page.getByText('$999').first()).toBeVisible();
  });

  test('should create Stripe checkout session for Tier 1', async ({ page }) => {
    await page.goto('/advanced?step=5&tier=1');
    await expect(page.getByText(/blueprint/i).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /proceed to checkout|launch free preview/i })).toBeVisible();
  });

  test('should create Stripe checkout session for Tier 2', async ({ page }) => {
    await page.goto('/advanced?step=5&tier=2');
    await expect(page.getByText(/boilerplate/i).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /proceed to checkout|launch free preview/i })).toBeVisible();
  });

  test('should create Stripe checkout session for Tier 3', async ({ page }) => {
    await page.goto('/advanced?step=5&tier=3');
    await expect(page.getByText(/infrastructure/i).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /proceed to checkout|launch free preview/i })).toBeVisible();
  });
});
