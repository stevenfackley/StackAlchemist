"use client";

import { useState } from "react";
import Link from "next/link";
import { Menu, X } from "lucide-react";
import { Logo } from "./logo";
import { useDismissable } from "@/lib/hooks/use-dismissable";

const NAV_LINKS: Array<{ label: string; href: string }> = [
  { label: "Pricing", href: "/pricing" },
  { label: "Blog", href: "/blog" },
  { label: "About", href: "/about" },
  { label: "Build", href: "/" },
];

/**
 * Shared header for /blog, /compare, /solutions, /faq and the static
 * marketing surfaces. Presentation-only — no server data dependencies.
 */
export function ContentHeader() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  // null = ESC-only: nav and mobile menu are DOM siblings so there's no single
  // container ref they can share. Click-outside is handled by the menu links.
  useDismissable(null, () => setMobileMenuOpen(false), mobileMenuOpen);

  return (
    <header className="border-b border-slate-surface bg-void/80 backdrop-blur-md sticky top-0 z-header">
      <nav className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
        <Logo />

        {/* Desktop nav */}
        <div className="hidden md:flex items-center gap-6">
          {NAV_LINKS.map(({ label, href }) => (
            <Link
              key={href}
              href={href}
              className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase"
            >
              {label}
            </Link>
          ))}
          <Link
            href="/login"
            className="text-xs font-mono tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-3 py-1.5 uppercase"
          >
            Login
          </Link>
        </div>

        {/* Mobile hamburger */}
        <button
          className="md:hidden p-2 text-slate-400 hover:text-white transition-colors"
          onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
          aria-label={mobileMenuOpen ? "Close menu" : "Open menu"}
          aria-expanded={mobileMenuOpen}
        >
          {mobileMenuOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </nav>

      {/* Mobile menu */}
      {mobileMenuOpen && (
        <div className="md:hidden bg-void/95 backdrop-blur-md border-b border-slate-surface px-4 py-4 flex flex-col gap-4">
          {NAV_LINKS.map(({ label, href }) => (
            <Link
              key={href}
              href={href}
              className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase"
              onClick={() => setMobileMenuOpen(false)}
            >
              {label}
            </Link>
          ))}
          <Link
            href="/login"
            className="text-xs font-mono tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-3 py-1.5 uppercase text-center"
            onClick={() => setMobileMenuOpen(false)}
          >
            Login
          </Link>
        </div>
      )}
    </header>
  );
}
