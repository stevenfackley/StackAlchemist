import { test } from '@playwright/test';

test.describe('Dashboard', () => {
  test('should display generation history', async ({ page }) => {
    await page.goto('/');
    test.skip(true, 'Scaffold: implement when dashboard exists');
  });

  test('should allow downloading completed generations', async ({ page }) => {
    await page.goto('/');
    test.skip(true, 'Scaffold: implement when download flow exists');
  });

  test('should display BYOK settings panel', async ({ page }) => {
    await page.goto('/');
    test.skip(true, 'Scaffold: implement when BYOK settings exist');
  });

  test('should save and mask BYOK API key', async ({ page }) => {
    await page.goto('/');
    test.skip(true, 'Scaffold: implement when BYOK settings exist');
  });
});
