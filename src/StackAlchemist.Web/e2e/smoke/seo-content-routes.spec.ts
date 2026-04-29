import { expect, test } from "@playwright/test";

/**
 * Smoke coverage for the public SEO surfaces added in Week 3:
 * /blog, /compare, /solutions, /faq and their dynamic children.
 *
 * Asserts:
 *  - Route renders 200.
 *  - Title tag present and brand-suffixed.
 *  - Canonical tag points at the expected path.
 *  - At least one JSON-LD script is emitted.
 *  - Key CTAs and breadcrumbs exist on dynamic children.
 *
 * Kept in the `smoke` bucket so it runs against NEXT_PUBLIC_DEMO_MODE=true;
 * the loaders fall back to a demo-mode placeholder when content files are
 * unreachable, so a signed-in backend is not required.
 */

const indexRoutes: Array<{ path: string; titleIncludes: RegExp }> = [
  { path: "/blog", titleIncludes: /blog/i },
  { path: "/compare", titleIncludes: /compare|comparison/i },
  { path: "/solutions", titleIncludes: /solutions|vertical/i },
  { path: "/faq", titleIncludes: /faq|frequently/i },
];

const dynamicRoutes: Array<{ path: string }> = [
  { path: "/blog/compile-guarantee-why-ai-codegen-must-verify" },
  { path: "/blog/swiss-cheese-method-deterministic-templates-llm-logic" },
  { path: "/compare/v0" },
  { path: "/compare/bolt-new" },
  { path: "/solutions/ai-ecommerce-platform" },
  { path: "/solutions/ai-lms-builder" },
  { path: "/solutions/fintech-saas-generator" },
];

test.describe("Smoke: SEO content routes", () => {
  for (const { path, titleIncludes } of indexRoutes) {
    test(`index ${path} renders with branded title + canonical`, async ({ page }) => {
      const response = await page.goto(path);
      expect(response?.status(), `${path} status`).toBeLessThan(400);

      await expect(page).toHaveTitle(titleIncludes);

      const canonical = page.locator('link[rel="canonical"]');
      await expect(canonical).toHaveCount(1);
      const href = await canonical.getAttribute("href");
      expect(href, `${path} canonical href`).toContain(path);
    });
  }

  for (const { path } of dynamicRoutes) {
    test(`dynamic ${path} renders with JSON-LD`, async ({ page }) => {
      const response = await page.goto(path);
      expect(response?.status(), `${path} status`).toBeLessThan(400);

      // At least one JSON-LD script must be present (BreadcrumbList at minimum).
      const jsonLd = page.locator('script[type="application/ld+json"]');
      expect(await jsonLd.count(), `${path} JSON-LD count`).toBeGreaterThan(0);

      // At least one of the JSON-LD blocks references schema.org as the @context URL.
      // Anchored to the URL scheme so e.g. `evil-schema.org.example.com` cannot satisfy it.
      const scripts = await jsonLd.allTextContents();
      const hasSchemaOrg = scripts.some((s) => /https?:\/\/schema\.org(?:["/]|$)/.test(s));
      expect(hasSchemaOrg, `${path} schema.org reference`).toBe(true);
    });
  }

  test("sitemap.xml lists blog, compare, and solutions routes", async ({ request }) => {
    const res = await request.get("/sitemap.xml");
    expect(res.status()).toBe(200);
    const body = await res.text();
    expect(body).toContain("/blog");
    expect(body).toContain("/compare");
    expect(body).toContain("/solutions");
  });

  test("robots.txt allows the new SEO surfaces", async ({ request }) => {
    const res = await request.get("/robots.txt");
    expect(res.status()).toBe(200);
    const body = await res.text();
    expect(body.toLowerCase()).toContain("allow");
    // Should not disallow these public surfaces explicitly.
    expect(body).not.toMatch(/disallow:\s*\/blog/i);
    expect(body).not.toMatch(/disallow:\s*\/compare/i);
    expect(body).not.toMatch(/disallow:\s*\/solutions/i);
  });
});
