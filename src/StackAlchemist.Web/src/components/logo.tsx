import Image from "next/image";
import Link from "next/link";

export function Logo({ size = 32 }: { size?: number }) {
  return (
    <Link href="/" className="flex items-center gap-2 group">
      <Image
        src="/logo.svg"
        alt="StackAlchemist"
        width={size}
        height={size}
        className="shrink-0"
      />
      <span className="font-mono text-sm font-semibold tracking-wider text-white group-hover:text-electric transition-colors">
        StackAlchemist
      </span>
    </Link>
  );
}
