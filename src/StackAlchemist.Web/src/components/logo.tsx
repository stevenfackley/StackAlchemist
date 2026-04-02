import Link from "next/link";
import Image from "next/image";

export function Logo({ className }: { className?: string }) {
  return (
    <Link href="/" className={`flex items-center gap-2.5 transition-opacity hover:opacity-80 ${className ?? ""}`}>
      <Image
        src="/logo.svg"
        alt="Stack Alchemist"
        width={32}
        height={32}
        className="drop-shadow-[0_0_8px_rgba(59,130,246,0.4)]"
        priority
      />
      <span className="font-mono text-sm font-medium tracking-widest text-slate-200 hidden sm:block">
        STACK <span className="text-blue-400">AL</span>CHEMIST
      </span>
    </Link>
  );
}
