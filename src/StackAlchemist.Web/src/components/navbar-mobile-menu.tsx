"use client";

import { useState } from "react";
import Link from "next/link";
import { LayoutDashboard, LogOut, Menu, X } from "lucide-react";

type NavbarMobileMenuProps = {
  userEmail: string | null;
  isAuthenticated: boolean;
};

export function NavbarMobileMenu({ userEmail, isAuthenticated }: NavbarMobileMenuProps) {
  const [open, setOpen] = useState(false);

  return (
    <div className="relative md:hidden">
      <button
        type="button"
        onClick={() => setOpen((current) => !current)}
        className="inline-flex items-center justify-center rounded-lg border border-slate-700/60 bg-slate-800/60 p-2 text-slate-300 transition-colors hover:border-electric/50 hover:text-electric"
        aria-label={open ? "Close navigation menu" : "Open navigation menu"}
        aria-expanded={open}
      >
        {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
      </button>

      {open && (
        <div className="absolute right-0 top-full mt-3 w-[min(18rem,calc(100vw-2rem))] rounded-2xl border border-slate-700/50 bg-void/95 p-4 shadow-[0_12px_48px_rgba(15,23,42,0.45)] backdrop-blur-md">
          <div className="flex flex-col gap-3">
            <Link
              href="/about"
              onClick={() => setOpen(false)}
              className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase"
            >
              About
            </Link>
            <Link
              href="/pricing"
              onClick={() => setOpen(false)}
              className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase"
            >
              Pricing
            </Link>
            <Link
              href="/story"
              onClick={() => setOpen(false)}
              className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase"
            >
              Our Story
            </Link>

            <div className="border-t border-slate-700/40 pt-3 space-y-3">
              {isAuthenticated ? (
                <>
                  {userEmail && (
                    <p className="font-mono text-[10px] text-slate-500 truncate">
                      {userEmail}
                    </p>
                  )}
                  <Link
                    href="/dashboard"
                    onClick={() => setOpen(false)}
                    className="inline-flex items-center gap-1.5 text-xs font-mono tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-3 py-1.5 uppercase"
                  >
                    <LayoutDashboard className="h-3 w-3" />
                    Dashboard
                  </Link>
                  <form action="/auth/signout" method="POST">
                    <button
                      type="submit"
                      className="inline-flex items-center gap-1.5 text-xs font-mono tracking-widest text-slate-400 hover:text-rose-400 transition-colors uppercase"
                    >
                      <LogOut className="h-3.5 w-3.5" />
                      Sign Out
                    </button>
                  </form>
                </>
              ) : (
                <Link
                  href="/login"
                  onClick={() => setOpen(false)}
                  className="inline-flex w-fit items-center justify-center text-xs font-mono tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-3 py-1.5 uppercase"
                >
                  Login
                </Link>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
