import Link from "next/link";
import Image from "next/image";

export function Logo({ className }: { className?: string }) {
  return (
    <Link href="/" className={`flex items-center gap-3 transition-opacity hover:opacity-80 ${className ?? ""}`}>
      <Image
        src="/logo.svg"
        alt="Stack Alchemist"
        width={34}
        height={34}
        className="drop-shadow-[0_0_10px_rgba(59,152,255,0.5)]"
        priority
      />
      <span className="text-sm font-semibold tracking-[0.18em] text-slate-100 uppercase">
        Stack&nbsp;&nbsp;<span className="text-[#4da6ff]">Al</span>chemist
      </span>
    </Link>
  );
}
