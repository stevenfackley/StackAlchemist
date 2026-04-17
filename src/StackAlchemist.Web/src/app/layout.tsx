import type { Metadata, Viewport } from "next";
import { Inter } from "next/font/google";
import { JetBrains_Mono } from "next/font/google";
import Script from "next/script";
import "./globals.css";
import { TestSiteBanner } from "@/components/test-site-banner";
import { softwareApplicationJsonLd, organizationJsonLd, websiteJsonLd } from "@/lib/jsonld";

const inter = Inter({
  subsets: ["latin"],
  variable: "--font-inter",
  display: "swap",
});

const jetbrainsMono = JetBrains_Mono({
  subsets: ["latin"],
  variable: "--font-jetbrains",
  display: "swap",
});

import { SITE_URL } from "@/lib/constants";
const siteUrl = new URL(SITE_URL);
const isTestSite = process.env.NEXT_PUBLIC_IS_TEST_SITE === "true";
const plausibleDomain = process.env.NEXT_PUBLIC_PLAUSIBLE_DOMAIN;
const googleVerification = process.env.GOOGLE_SITE_VERIFICATION;

const defaultDescription =
  "StackAlchemist is the AI SaaS generator with a 100% compile guarantee. Turn natural language into production-ready .NET 10 + Next.js 15 + PostgreSQL repositories you own for a one-time price — no subscriptions, no lock-in.";

export const metadata: Metadata = {
  metadataBase: siteUrl,
  title: {
    default: "StackAlchemist — AI SaaS Generator with a Compile Guarantee",
    template: "%s | StackAlchemist",
  },
  description: defaultDescription,
  applicationName: "StackAlchemist",
  keywords: [
    "AI SaaS generator",
    "AI app builder",
    "AI code generator",
    "v0 alternative",
    "Bolt.new alternative",
    "Lovable alternative",
    "full-stack AI codegen",
    ".NET 10",
    "Next.js 15",
    "Supabase",
    "Swiss Cheese Method",
    "compile guarantee",
    "SaaS boilerplate",
    "one-time payment AI builder",
  ],
  authors: [{ name: "StackAlchemist" }],
  creator: "StackAlchemist",
  publisher: "StackAlchemist",
  icons: {
    icon: "/favicon.svg",
  },
  // Per-page canonicals are set in each page.tsx via alternates.canonical.
  // Intentionally NOT set here — otherwise every child page would inherit "/".
  openGraph: {
    // Images are served via app/opengraph-image.tsx (file convention).
    // Do not set `images` here — Next.js auto-injects the dynamic OG URL.
    type: "website",
    url: siteUrl.toString(),
    title: "StackAlchemist — AI SaaS Generator with a Compile Guarantee",
    description: defaultDescription,
    siteName: "StackAlchemist",
  },
  twitter: {
    // Twitter card image is auto-derived from the OG image convention.
    card: "summary_large_image",
    title: "StackAlchemist — AI SaaS Generator with a Compile Guarantee",
    description: defaultDescription,
  },
  // Test mirror must never be indexed (avoid duplicate-content split with prod)
  robots: isTestSite
    ? { index: false, follow: false, nocache: true, googleBot: { index: false, follow: false } }
    : { index: true, follow: true },
  verification: googleVerification ? { google: googleVerification } : undefined,
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  maximumScale: 5,
  viewportFit: "cover",
};

const siteUrlStr = siteUrl.toString();

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" className={`${inter.variable} ${jetbrainsMono.variable} dark`}>
        <body className="min-h-screen bg-void text-white font-sans antialiased">
          <TestSiteBanner />
          <script
            type="application/ld+json"
            dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationJsonLd(siteUrlStr, defaultDescription)) }}
          />
          <script
            type="application/ld+json"
            dangerouslySetInnerHTML={{ __html: JSON.stringify(organizationJsonLd(siteUrlStr)) }}
          />
          <script
            type="application/ld+json"
            dangerouslySetInnerHTML={{ __html: JSON.stringify(websiteJsonLd(siteUrlStr, defaultDescription)) }}
          />
          {plausibleDomain && !isTestSite ? (
            <Script
              src="https://plausible.io/js/script.js"
              data-domain={plausibleDomain}
              strategy="afterInteractive"
              defer
            />
          ) : null}
          {children}
        </body>
    </html>
  );
}
