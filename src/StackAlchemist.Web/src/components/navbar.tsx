import Link from "next/link";
import { Logo } from "./logo";

export function Navbar() {
  return (
    <header className="border-b border-slate-surface bg-void/80 backdrop-blur-md sticky top-0 z-50">
      <nav className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
        <Logo />
        <div className="flex items-center gap-6">
          <Link
            href="#features"
            className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase"
          >
            Features
          </Link>
          <Link
            href="#pricing"
            className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase"
          >
            Pricing
          </Link>
          <Link
            href="/login"
            className="text-xs font-mono tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-3 py-1.5 uppercase"
          >
            Login
          </Link>
        </div>
      </nav>
    </header>
  );
}
