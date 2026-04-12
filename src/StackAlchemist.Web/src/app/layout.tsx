import type { Metadata, Viewport } from "next";
import { Inter } from "next/font/google";
import { JetBrains_Mono } from "next/font/google";
import "./globals.css";
import { TestSiteBanner } from "@/components/test-site-banner";

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

const siteUrl = new URL(process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000");

export const metadata: Metadata = {
  metadataBase: siteUrl,
  title: {
    default: "StackAlchemist",
    template: "%s | StackAlchemist",
  },
  description:
    "The 100% Build-Guaranteed SaaS scaffolder. Convert natural language into fully compiled, downloadable code repositories.",
  icons: {
    icon: "/favicon.svg",
  },
  openGraph: {
    type: "website",
    title: "StackAlchemist",
    description:
      "The 100% Build-Guaranteed SaaS scaffolder. Convert natural language into fully compiled, downloadable code repositories.",
    siteName: "StackAlchemist",
    images: ["/og-image.png"],
  },
  twitter: {
    card: "summary_large_image",
    title: "StackAlchemist",
    description:
      "The 100% Build-Guaranteed SaaS scaffolder. Convert natural language into fully compiled, downloadable code repositories.",
    images: ["/og-image.png"],
  },
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  maximumScale: 5,
  viewportFit: "cover",
};

const softwareApplicationJsonLd = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "StackAlchemist",
  applicationCategory: "DeveloperApplication",
  operatingSystem: "Web",
  description:
    "The 100% Build-Guaranteed SaaS scaffolder. Convert natural language into fully compiled, downloadable code repositories.",
  url: siteUrl.toString(),
  offers: {
    "@type": "Offer",
    price: "0",
    priceCurrency: "USD",
  },
};

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
            dangerouslySetInnerHTML={{
              __html: JSON.stringify(softwareApplicationJsonLd),
            }}
          />
          {children}
        </body>
    </html>
  );
}
