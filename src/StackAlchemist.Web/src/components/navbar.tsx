import Link from "next/link";
import { Logo } from "./logo";
import { getServerUser } from "@/lib/supabase-server";
import { LayoutDashboard, LogOut } from "lucide-react";

/**
 * Phase 6 — Navbar is now an async Server Component.
 *
 * Reads the authenticated user from the SSR Supabase client on every render
 * (no client-side state needed).  When a user is signed in:
 *  - "Login" link is replaced by a "Dashboard" link + user email badge
 *  - A sign-out button POSTs to /auth/signout (Route Handler)
 *
 * Falls back gracefully to the unauthenticated layout when Supabase is not
 * configured (demo mode / CI).
 */
export async function Navbar() {
  const user = await getServerUser();

  return (
    <header className="border-b border-slate-surface bg-void/80 backdrop-blur-md sticky top-0 z-50">
      <nav className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
        <Logo />

        <div className="flex items-center gap-6">
          <Link
            href="/about"
            className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase"
          >
            About
          </Link>
          <Link
            href="/pricing"
            className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase"
          >
            Pricing
          </Link>
          <Link
            href="/story"
            className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase"
          >
            Our Story
          </Link>

          {user ? (
            /* ── Authenticated state ─────────────────────────────────────── */
            <div className="flex items-center gap-3">
              {/* Email badge (desktop only) */}
              <span className="hidden md:block font-mono text-[10px] text-slate-500 max-w-[160px] truncate">
                {user.email}
              </span>

              {/* Dashboard link */}
              <Link
                href="/dashboard"
                className="flex items-center gap-1.5 text-xs font-mono tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-3 py-1.5 uppercase"
              >
                <LayoutDashboard className="h-3 w-3" />
                <span className="hidden sm:block">Dashboard</span>
              </Link>

              {/* Sign-out — POST to the Route Handler */}
              <form action="/auth/signout" method="POST">
                <button
                  type="submit"
                  className="flex items-center gap-1.5 text-xs font-mono tracking-widest text-slate-400 hover:text-rose-400 transition-colors uppercase"
                  title="Sign out"
                >
                  <LogOut className="h-3.5 w-3.5" />
                  <span className="hidden sm:block">Sign Out</span>
                </button>
              </form>
            </div>
          ) : (
            /* ── Anonymous state ─────────────────────────────────────────── */
            <Link
              href="/login"
              className="text-xs font-mono tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-3 py-1.5 uppercase"
            >
              Login
            </Link>
          )}
        </div>
      </nav>
    </header>
  );
}
