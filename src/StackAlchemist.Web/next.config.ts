import type { NextConfig } from "next";
import { execSync } from "child_process";

// Capture the last git commit date at build time so the test-site banner
// can display it without any runtime overhead.
function getGitCommitDate(): string {
  try {
    return execSync("git log -1 --format=%ci", { encoding: "utf8" }).trim();
  } catch {
    return new Date().toISOString();
  }
}

const nextConfig: NextConfig = {
  env: {
    NEXT_PUBLIC_BUILD_DATE: getGitCommitDate(),
  },
  images: {
    formats: ["image/avif", "image/webp"],
  },
  output: "standalone",
  outputFileTracingRoot: __dirname,
  webpack: (config) => {
    // Fix Windows + pnpm symlink casing collision:
    // pnpm uses a virtual store with symlinks; webpack follows those symlinks and
    // ends up with both "C:\..." and "c:\..." paths for the same file, causing
    // next/dist/pages/_document to load twice and breaking the <Html> singleton
    // check during static prerendering of /404.
    // Setting symlinks:false makes webpack use the symlink path as-is (consistent
    // casing) rather than resolving through to the virtual store.
    config.resolve.symlinks = false;
    return config;
  },
  async headers() {
    const isTestSite = process.env.NEXT_PUBLIC_IS_TEST_SITE === "true";

    // CSP in Report-Only mode first so we see violations without blocking users.
    // Allowlist: Stripe (JS + API), Supabase (auth + DB REST + realtime WS),
    // Plausible analytics, Cloudflare Insights beacon, self for everything else.
    // Flip to enforce in Batch C once reports are clean.
    const cspReportOnly = [
      "default-src 'self'",
      "base-uri 'self'",
      "frame-ancestors 'self'",
      "form-action 'self'",
      // Next.js inlines runtime chunks; unsafe-inline needed until we adopt nonces.
      "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://js.stripe.com https://plausible.io https://static.cloudflareinsights.com",
      "style-src 'self' 'unsafe-inline'",
      "img-src 'self' data: blob: https:",
      "font-src 'self' data:",
      "connect-src 'self' https://*.supabase.co wss://*.supabase.co https://api.stripe.com https://plausible.io https://cloudflareinsights.com",
      "frame-src 'self' https://js.stripe.com https://hooks.stripe.com",
      "worker-src 'self' blob:",
      "object-src 'none'",
      "upgrade-insecure-requests",
      "report-uri /api/csp-report",
    ].join("; ");

    const baseHeaders = [
      {
        // Baseline security headers. Applied to every path so CF edge + nginx
        // forwarding preserve them consistently. HSTS is safe even on the test
        // mirror — both sites live under *.stackalchemist.app with HTTPS only.
        source: "/:path*",
        headers: [
          { key: "Strict-Transport-Security", value: "max-age=63072000; includeSubDomains; preload" },
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "X-Frame-Options", value: "SAMEORIGIN" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), browsing-topics=()" },
          { key: "Content-Security-Policy-Report-Only", value: cspReportOnly },
        ],
      },
      {
        // Long-lived immutable cache for user-shipped static assets under /public.
        // Next already handles /_next/static; this covers our own /images and /fonts.
        source: "/images/:file*",
        headers: [{ key: "Cache-Control", value: "public, max-age=31536000, immutable" }],
      },
      {
        source: "/fonts/:file*",
        headers: [{ key: "Cache-Control", value: "public, max-age=31536000, immutable" }],
      },
      {
        // StackBlitz WebContainers requires cross-origin isolation (SharedArrayBuffer).
        // Apply COOP + COEP headers to the generate result page only.
        source: "/generate/:id*",
        headers: [
          {
            key: "Cross-Origin-Opener-Policy",
            value: "same-origin",
          },
          {
            key: "Cross-Origin-Embedder-Policy",
            value: "require-corp",
          },
        ],
      },
    ];

    // Defence-in-depth noindex on the test mirror: the layout already emits a
    // meta robots tag, but some crawlers honour the response header only.
    if (isTestSite) {
      baseHeaders.push({
        source: "/:path*",
        headers: [
          {
            key: "X-Robots-Tag",
            value: "noindex, nofollow",
          },
        ],
      });
    }

    return baseHeaders;
  },
};

export default nextConfig;
