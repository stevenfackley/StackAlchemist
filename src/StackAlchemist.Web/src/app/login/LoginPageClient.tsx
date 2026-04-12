"use client";

import { Suspense, useState, useTransition } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
import { Loader2, AlertCircle, CheckCircle2 } from "lucide-react";
import { supabase } from "@/lib/supabase";
import { isDemoMode } from "@/lib/runtime-config";

// Inner component isolated so useSearchParams() is inside the Suspense boundary.
function LoginPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnTo = searchParams?.get("returnTo") ?? "/";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [mode, setMode] = useState<"password" | "magic">("password");
  const [status, setStatus] = useState<"idle" | "success" | "error">("idle");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    startTransition(async () => {
      setStatus("idle");
      setErrorMsg(null);

      if (isDemoMode || !supabase) {
        // Demo mode: skip auth and redirect
        router.push(returnTo);
        return;
      }

      if (mode === "magic") {
        const { error } = await supabase.auth.signInWithOtp({
          email: email.trim(),
          options: {
            emailRedirectTo: `${window.location.origin}/auth/callback?next=${encodeURIComponent(returnTo)}`,
          },
        });
        if (error) {
          setErrorMsg(error.message);
          setStatus("error");
        } else {
          setStatus("success");
        }
      } else {
        const { error } = await supabase.auth.signInWithPassword({
          email: email.trim(),
          password,
        });
        if (error) {
          setErrorMsg(error.message);
          setStatus("error");
        } else {
          router.push(returnTo);
          router.refresh();
        }
      }
    });
  }

  return (
    <div className="min-h-screen flex flex-col bg-slate-800">
      {/* Header */}
      <header className="border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0 z-50">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center gap-4">
          <Link href="/" className="flex items-center gap-2.5 transition-opacity hover:opacity-80">
            <Image src="/logo.svg" alt="Stack Alchemist" width={28} height={28} className="drop-shadow-[0_0_6px_rgba(59,130,246,0.4)]" />
            <span className="font-mono text-sm font-medium tracking-widest text-slate-200 hidden sm:block">
              STACK <span className="text-blue-400">AL</span>CHEMIST
            </span>
          </Link>
        </div>
      </header>

      <main className="flex-1 flex items-center justify-center px-4 py-16">
        <div className="w-full max-w-md space-y-6">
          {/* Title */}
          <div className="text-center space-y-2">
            <h1 className="text-2xl font-bold text-white tracking-tight">Sign In</h1>
            <p className="text-slate-400 text-sm">
              Track your generations and access your downloads.
            </p>
          </div>

          {/* Card */}
          <div className="rounded-2xl border border-slate-600/40 bg-slate-700/20 p-8 space-y-6">
            {/* Mode toggle */}
            <div className="flex rounded-xl border border-slate-600/40 overflow-hidden">
              <button
                type="button"
                onClick={() => setMode("password")}
                className={`flex-1 font-mono text-xs py-2 uppercase tracking-widest transition-colors ${
                  mode === "password"
                    ? "bg-blue-500 text-white"
                    : "bg-transparent text-slate-400 hover:text-white"
                }`}
              >
                Password
              </button>
              <button
                type="button"
                onClick={() => setMode("magic")}
                className={`flex-1 font-mono text-xs py-2 uppercase tracking-widest transition-colors ${
                  mode === "magic"
                    ? "bg-blue-500 text-white"
                    : "bg-transparent text-slate-400 hover:text-white"
                }`}
              >
                Magic Link
              </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-4">
              {/* Email */}
              <div className="space-y-1.5">
                <label className="font-mono text-xs text-slate-400 uppercase tracking-widest">
                  Email
                </label>
                <input
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="you@example.com"
                  className="w-full bg-slate-800/60 border border-slate-600/40 rounded-xl px-4 py-3 font-mono text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 transition-colors"
                />
              </div>

              {/* Password (password mode only) */}
              {mode === "password" && (
                <div className="space-y-1.5">
                  <label className="font-mono text-xs text-slate-400 uppercase tracking-widest">
                    Password
                  </label>
                  <input
                    type="password"
                    required
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="••••••••"
                    className="w-full bg-slate-800/60 border border-slate-600/40 rounded-xl px-4 py-3 font-mono text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 transition-colors"
                  />
                </div>
              )}

              {/* Status messages */}
              {status === "error" && errorMsg && (
                <div className="flex items-start gap-2.5 rounded-xl border border-rose-500/30 bg-rose-500/10 px-4 py-3">
                  <AlertCircle className="h-4 w-4 text-rose-400 shrink-0 mt-0.5" />
                  <p className="font-mono text-xs text-rose-300">{errorMsg}</p>
                </div>
              )}

              {status === "success" && mode === "magic" && (
                <div className="flex items-start gap-2.5 rounded-xl border border-emerald-500/30 bg-emerald-500/10 px-4 py-3">
                  <CheckCircle2 className="h-4 w-4 text-emerald-400 shrink-0 mt-0.5" />
                  <p className="font-mono text-xs text-emerald-300">
                    Magic link sent! Check your inbox and click the link to sign in.
                  </p>
                </div>
              )}

              {/* Submit */}
              <button
                type="submit"
                disabled={isPending}
                className="w-full flex items-center justify-center gap-2 bg-blue-500 hover:bg-blue-400 disabled:opacity-60 text-white font-mono text-xs py-3 rounded-xl uppercase tracking-widest transition-colors"
              >
                {isPending ? (
                  <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Signing in...</>
                ) : mode === "magic" ? (
                  "Send Magic Link"
                ) : (
                  "Sign In"
                )}
              </button>
            </form>

            {/* Divider */}
            <div className="flex items-center gap-3">
              <div className="flex-1 h-px bg-slate-600/40" />
              <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">or</span>
              <div className="flex-1 h-px bg-slate-600/40" />
            </div>

            {/* Register link */}
            <p className="text-center font-mono text-xs text-slate-400">
              Don&apos;t have an account?{" "}
              <Link
                href={`/register${returnTo !== "/" ? `?returnTo=${encodeURIComponent(returnTo)}` : ""}`}
                className="text-blue-400 hover:text-blue-300 transition-colors underline underline-offset-2"
              >
                Create one
              </Link>
            </p>
          </div>

          {/* Skip */}
          <p className="text-center">
            <Link href="/" className="font-mono text-xs text-slate-500 hover:text-slate-400 transition-colors tracking-widest uppercase">
              Continue without account →
            </Link>
          </p>
        </div>
      </main>
    </div>
  );
}

// Suspense boundary required by Next.js 15 whenever useSearchParams() is used
// in a component that could be statically prerendered.
export default function LoginPage() {
  return (
    <Suspense>
      <LoginPageContent />
    </Suspense>
  );
}
