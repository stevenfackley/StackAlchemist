import Link from "next/link";
import Image from "next/image";

export function HomeFooter() {
  return (
    <footer className="relative z-10 border-t border-slate-600/30 py-10 px-6 sm:px-8 lg:px-16">
      <div className="mx-auto max-w-6xl">
        <div className="flex flex-col sm:flex-row items-center justify-between gap-6">
          <div className="flex items-center gap-3">
            <Image
              src="/logo.svg"
              alt="Stack Alchemist"
              width={28}
              height={28}
              className="opacity-60"
            />
            <span className="font-mono text-xs tracking-widest text-slate-500">
              STACK <span className="text-accent/60">AL</span>CHEMIST
            </span>
          </div>
          <div className="flex flex-wrap items-center justify-center gap-6">
            <Link href="/pricing" className="font-mono text-xs text-slate-300 hover:text-white transition-colors uppercase tracking-widest">Pricing</Link>
            <Link href="/about" className="font-mono text-xs text-slate-300 hover:text-white transition-colors uppercase tracking-widest">About</Link>
            <Link href="/story" className="font-mono text-xs text-slate-300 hover:text-white transition-colors uppercase tracking-widest">Story</Link>
            <Link href="/docs" className="font-mono text-xs text-slate-300 hover:text-white transition-colors uppercase tracking-widest">Docs</Link>
            <Link href="/privacy" className="font-mono text-xs text-slate-300 hover:text-white transition-colors uppercase tracking-widest">Privacy</Link>
            <Link href="/terms" className="font-mono text-xs text-slate-300 hover:text-white transition-colors uppercase tracking-widest">Terms</Link>
            <Link href="/contact" className="font-mono text-xs text-slate-300 hover:text-white transition-colors uppercase tracking-widest">Contact</Link>
          </div>
          <p className="font-mono text-xs text-slate-400">
            &copy; {new Date().getFullYear()} StackAlchemist
          </p>
        </div>
      </div>
    </footer>
  );
}
