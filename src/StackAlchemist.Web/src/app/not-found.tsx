import Link from "next/link";

export default function NotFound() {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center bg-slate-800 px-4 text-center">
      <p className="font-mono text-xs tracking-[0.3em] text-blue-400 uppercase mb-4">404</p>
      <h1 className="text-3xl font-bold text-white mb-3">Page Not Found</h1>
      <p className="text-slate-400 text-sm mb-8 max-w-md">
        The page you&apos;re looking for doesn&apos;t exist or has been moved.
      </p>
      <Link
        href="/"
        className="font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white px-6 py-3 rounded-full uppercase tracking-widest transition-colors"
      >
        Back to Home
      </Link>
    </div>
  );
}
