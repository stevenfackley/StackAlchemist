"use client";

import { Suspense, useState, useTransition } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
import { Loader2, AlertCircle, CheckCircle2 } from "lucide-react";
import { supabase } from "@/lib/supabase";
import { isDemoMode } from "@/lib/runtime-config";

// Inner component isolated so useSearchParams() is inside the Suspense boundary.
function RegisterPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnTo = searchParams?.get("returnTo") ?? "/";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [status, setStatus] = useState<"idle" | "success" | "error">("idle");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    startTransition(async () => {
      setStatus("idle");
      setErrorMsg(null);

      if (password !== confirmPassword) {
        setErrorMsg("Passwords do not match.");
        setStatus("error");
        return;
      }

      if (password.length < 8) {
        setErrorMsg("Password must be at least 8 characters.");
        setStatus("error");
        return;
      }

      if (isDemoMode || !supabase) {
        // Demo mode: skip auth
        router.push(returnTo);
        return;
      }

      const { error } = await supabase.auth.signUp({
        email: email.trim(),
        password,
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
    });
  }

  if (status === "success") {
    return (
      <div className="min-h-screen flex flex-col bg-slate-800">
        <header className="border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0 z-50">
          <div className="max-w-6xl mx-auto px-4 h-14 flex items-center">
            <Link href="/" className="flex items-center gap-2.5 transition-opacity hover:opacity-80">
              <Image src="/logo.svg" alt="Stack Alchemist" width={28} height={28} className="drop-shadow-[0_0_6px_rgba(59,130,246,0.4)]" />
              <span className="font-mono text-sm font-medium tracking-widest text-slate-200 hidden sm:block">
                STACK <span className="text-blue-400">AL</span>CHEMIST
              </span>
            </Link>
          </div>
        </header>
        <main className="flex-1 flex items-center justify-center px-4 py-16">
          <div className="w-full max-w-md text-center space-y-6">
            <div className="h-16 w-16 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center mx-auto">
              <CheckCircle2 className="h-8 w-8 text-emerald-400" />
            </div>
            <div>
              <h2 className="text-2xl font-bold text-white mb-2">Check your inbox</h2>
              <p className="text-slate-400 text-sm leading-relaxed">
                We&apos;ve sent a confirmation link to <span className="text-white font-medium">{email}</span>.
                Click the link to activate your account and sign in.
              </p>
            </div>
            <Link
              href="/"
              className="inline-block font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white px-6 py-3 rounded-xl uppercase tracking-widest transition-colors"
            >
              Back to Home
            </Link>
          </div>
        </main>
      </div>
    );
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
            <h2 className="text-2xl font-bold text-white tracking-tight">Create Account</h2>
            <p className="text-slate-400 text-sm">
              Free to register. Track your generations and retrieve downloads anytime.
            </p>
          </div>

          {/* Card */}
          <div className="rounded-2xl border border-slate-600/40 bg-slate-700/20 p-8 space-y-5">
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

              {/* Password */}
              <div className="space-y-1.5">
                <label className="font-mono text-xs text-slate-400 uppercase tracking-widest">
                  Password
                </label>
                <input
                  type="password"
                  required
                  minLength={8}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Min. 8 characters"
                  className="w-full bg-slate-800/60 border border-slate-600/40 rounded-xl px-4 py-3 font-mono text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 transition-colors"
                />
              </div>

              {/* Confirm Password */}
              <div className="space-y-1.5">
                <label className="font-mono text-xs text-slate-400 uppercase tracking-widest">
                  Confirm Password
                </label>
                <input
                  type="password"
                  required
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="••••••••"
                  className="w-full bg-slate-800/60 border border-slate-600/40 rounded-xl px-4 py-3 font-mono text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 transition-colors"
                />
              </div>

              {/* Error */}
              {status === "error" && errorMsg && (
                <div className="flex items-start gap-2.5 rounded-xl border border-rose-500/30 bg-rose-500/10 px-4 py-3">
                  <AlertCircle className="h-4 w-4 text-rose-400 shrink-0 mt-0.5" />
                  <p className="font-mono text-xs text-rose-300">{errorMsg}</p>
                </div>
              )}

              {/* Submit */}
              <button
                type="submit"
                disabled={isPending}
                className="w-full flex items-center justify-center gap-2 bg-blue-500 hover:bg-blue-400 disabled:opacity-60 text-white font-mono text-xs py-3 rounded-xl uppercase tracking-widest transition-colors"
              >
                {isPending ? (
                  <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Creating account...</>
                ) : (
                  "Create Account"
                )}
              </button>
            </form>

            {/* Divider */}
            <div className="flex items-center gap-3">
              <div className="flex-1 h-px bg-slate-600/40" />
              <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">or</span>
              <div className="flex-1 h-px bg-slate-600/40" />
            </div>

            {/* Login link */}
            <p className="text-center font-mono text-xs text-slate-400">
              Already have an account?{" "}
              <Link
                href={`/login${returnTo !== "/" ? `?returnTo=${encodeURIComponent(returnTo)}` : ""}`}
                className="text-blue-400 hover:text-blue-300 transition-colors underline underline-offset-2"
              >
                Sign in
              </Link>
            </p>
          </div>

          {/* Privacy note */}
          <p className="text-center font-mono text-[10px] text-slate-600 leading-relaxed px-4">
            By creating an account you agree to our Terms of Service.
            We never sell or share your data.
          </p>
        </div>
      </main>
    </div>
  );
}

// Suspense boundary required by Next.js 15 whenever useSearchParams() is used
// in a component that could be statically prerendered.
export default function RegisterPage() {
  return (
    <>
      <h1 className="sr-only">Create Your StackAlchemist Account</h1>
      <Suspense>
        <RegisterPageContent />
      </Suspense>
    </>
  );
}
