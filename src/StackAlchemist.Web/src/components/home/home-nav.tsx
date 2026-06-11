"use client";

import { useState } from "react";
import Link from "next/link";
import Image from "next/image";
import { Menu, X } from "lucide-react";
import { useDismissable } from "@/lib/hooks/use-dismissable";

export function HomeNav() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  // null = ESC-only: nav and mobile menu are DOM siblings so there's no single
  // container ref they can share. Click-outside is handled by the menu links.
  useDismissable(null, () => setMobileMenuOpen(false), mobileMenuOpen);

  return (
    <>
      <nav className="relative z-50 flex items-center justify-between px-6 sm:px-8 lg:px-16 py-5 border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0">
        {/* Logo */}
        <Link href="/" className="flex items-center gap-3 transition-opacity hover:opacity-80">
          <Image
            src="/logo.svg"
            alt="Stack Alchemist"
            width={34}
            height={34}
            className="drop-shadow-[0_0_10px_rgba(77,166,255,0.5)]"
            priority
          />
          <span className="text-sm font-semibold tracking-[0.18em] text-slate-100 uppercase">
            Stack&nbsp;&nbsp;<span className="text-[#4da6ff]">Al</span>chemist
          </span>
        </Link>

        {/* Desktop nav */}
        <div className="hidden md:flex items-center gap-8">
          <Link href="/about" className="text-sm text-slate-300 hover:text-white transition-colors duration-300">About</Link>
          <a href="#pricing" className="text-sm text-slate-300 hover:text-white transition-colors duration-300">Pricing</a>
          <Link href="/story" className="text-sm text-slate-300 hover:text-white transition-colors duration-300">Story</Link>
          <Link href="/docs" className="text-sm text-slate-300 hover:text-white transition-colors duration-300">Docs</Link>
          <Link href="/login" className="rounded-full border border-slate-500/30 bg-slate-700/50 px-4 py-2 text-sm text-slate-100 backdrop-blur-sm transition-all duration-300 hover:-translate-y-0.5 hover:border-blue-500/40 hover:shadow-[0_0_20px_rgba(59,130,246,0.2)]">
            Sign In
          </Link>
        </div>

        {/* Mobile hamburger */}
        <button
          className="md:hidden p-2 text-slate-300 hover:text-white transition-colors"
          onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
          aria-label={mobileMenuOpen ? "Close menu" : "Open menu"}
          aria-expanded={mobileMenuOpen}
        >
          {mobileMenuOpen ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
        </button>
      </nav>

      {/* Mobile menu */}
      {mobileMenuOpen && (
        <div className="relative z-40 md:hidden bg-slate-800/95 backdrop-blur-md border-b border-slate-600/30 px-6 py-4 flex flex-col gap-4">
          <Link href="/about" className="text-sm text-slate-300 hover:text-white" onClick={() => setMobileMenuOpen(false)}>About</Link>
          <a href="#pricing" className="text-sm text-slate-300 hover:text-white" onClick={() => setMobileMenuOpen(false)}>Pricing</a>
          <Link href="/story" className="text-sm text-slate-300 hover:text-white" onClick={() => setMobileMenuOpen(false)}>Story</Link>
          <Link href="/docs" className="text-sm text-slate-300 hover:text-white" onClick={() => setMobileMenuOpen(false)}>Docs</Link>
          <Link href="/login" className="w-full text-center rounded-full border border-slate-500/30 bg-slate-700/50 px-4 py-2 text-sm text-slate-100 hover:border-blue-500/40 transition-colors" onClick={() => setMobileMenuOpen(false)}>
            Sign In
          </Link>
        </div>
      )}
    </>
  );
}
