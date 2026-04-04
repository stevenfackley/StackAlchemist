import type { Metadata } from "next";
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

export const metadata: Metadata = {
  title: "StackAlchemist — Transmute Prompts Into Architecture",
  description:
    "The 100% Build-Guaranteed SaaS Scaffolder. Convert natural language into fully compiled, downloadable code repositories.",
  icons: {
    icon: "/favicon.svg",
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
          {children}
        </body>
    </html>
  );
}
