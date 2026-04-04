import { test, expect } from '@playwright/test';

test.describe('Dashboard', () => {
  test('should redirect unauthenticated users to login', async ({ page }) => {
    // When an anonymous visitor hits /dashboard, they should be sent to /login
    // with a returnTo param pointing back to /dashboard.
    const res = await page.goto('/dashboard');
    await page.waitForURL(/\/login/);
    expect(page.url()).toContain('/login');
    expect(page.url()).toContain('returnTo');
  });

  test('login page should render sign-in form', async ({ page }) => {
    await page.goto('/login');
    // Must contain both the heading and the submit button
    await expect(page.getByRole('heading', { name: /sign in/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /sign in|send magic link/i })).toBeVisible();
  });

  test('login page should show magic-link toggle', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('button', { name: /magic link/i })).toBeVisible();
    // Clicking it hides the password field
    await page.getByRole('button', { name: /magic link/i }).click();
    await expect(page.getByPlaceholder(/••••••••/)).not.toBeVisible();
  });

  test('register page should render signup form', async ({ page }) => {
    await page.goto('/register');
    await expect(page.getByRole('heading', { name: /create account/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /create account/i })).toBeVisible();
  });

  test('register page client-validates mismatched passwords', async ({ page }) => {
    await page.goto('/register');
    await page.getByPlaceholder('you@example.com').fill('test@example.com');
    // Fill two different password values
    const [pass, confirm] = await page.getByPlaceholder(/min\. 8 characters|•/i).all();
    await pass.fill('Password1!');
    await confirm.fill('DifferentPass!');
    await page.getByRole('button', { name: /create account/i }).click();
    await expect(page.getByText(/passwords do not match/i)).toBeVisible();
  });

  test('should allow downloading completed generations', async ({ page }) => {
    await page.goto('/');
    test.skip(true, 'Scaffold: implement when download flow is wired end-to-end');
  });

  test('should display BYOK settings panel when authenticated', async ({ page }) => {
    await page.goto('/');
    test.skip(true, 'Scaffold: implement with authenticated test user in Phase 7');
  });
});
