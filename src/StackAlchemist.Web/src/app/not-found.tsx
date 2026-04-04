import Link from "next/link";
import Image from "next/image";

export default function NotFound() {
  return (
    <div className="min-h-screen bg-slate-800 flex flex-col items-center justify-center px-4">
      <div className="text-center space-y-6 max-w-md">
        <div className="flex justify-center">
          <Link href="/">
            <Image src="/logo.svg" alt="Stack Alchemist" width={48} height={48} className="opacity-60" />
          </Link>
        </div>
        <div className="space-y-2">
          <p className="font-mono text-xs tracking-[0.35em] text-slate-500 uppercase">404</p>
          <h1 className="text-3xl font-bold text-white">Page Not Found</h1>
          <p className="text-slate-400 text-sm leading-relaxed">
            This route is unavailable in the current environment. If you&apos;re using Bolt.new,
            demo mode is active so only the frontend experience is mocked.
          </p>
        </div>
        <Link
          href="/"
          className="inline-block rounded-full bg-blue-500 text-white px-6 py-2.5 text-sm font-medium hover:bg-blue-400 transition-all duration-300"
        >
          Return Home
        </Link>
      </div>
    </div>
  );
}