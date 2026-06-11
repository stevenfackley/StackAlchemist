"use client";

import { useState, useTransition } from "react";
import Link from "next/link";
import Image from "next/image";
import { CheckCircle2, Loader2 } from "lucide-react";
import { supabase } from "@/lib/supabase";
import { isDemoMode } from "@/lib/runtime-config";

export default function ForgotPasswordClient() {
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const [isPending, startTransition] = useTransition();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    startTransition(async () => {
      if (!isDemoMode && supabase) {
        await supabase.auth.resetPasswordForEmail(email.trim(), {
          redirectTo: `${window.location.origin}/auth/callback?next=${encodeURIComponent("/auth/reset-password")}`,
        });
      }
      // Always show success — don't reveal which emails are registered.
      setSent(true);
    });
  }

  const header = (
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
  );

  if (sent) {
    return (
      <div className="min-h-screen flex flex-col bg-slate-800">
        {header}
        <main className="flex-1 flex items-center justify-center px-4 py-16">
          <div className="w-full max-w-md text-center space-y-6">
            <div className="h-16 w-16 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center mx-auto">
              <CheckCircle2 className="h-8 w-8 text-emerald-400" />
            </div>
            <div>
              <h2 className="text-2xl font-bold text-white mb-2">Check your inbox</h2>
              <p className="text-slate-400 text-sm leading-relaxed">
                If an account exists for{" "}
                <span className="text-white font-medium">{email}</span>,
                you&apos;ll receive a reset link shortly.
              </p>
            </div>
            <Link
              href="/login"
              className="inline-block font-mono text-xs border border-slate-500 hover:border-slate-400 text-slate-300 hover:text-white px-6 py-3 rounded-xl uppercase tracking-widest transition-colors"
            >
              Back to Sign In
            </Link>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-slate-800">
      {header}
      <main className="flex-1 flex items-center justify-center px-4 py-16">
        <div className="w-full max-w-md space-y-6">
          <div className="text-center space-y-2">
            <h2 className="text-2xl font-bold text-white tracking-tight">Reset Password</h2>
            <p className="text-slate-400 text-sm">Enter your email and we&apos;ll send a reset link.</p>
          </div>

          <div className="rounded-2xl border border-slate-600/40 bg-slate-700/20 p-8 space-y-5">
            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="space-y-1.5">
                <label htmlFor="fp-email" className="font-mono text-xs text-slate-400 uppercase tracking-widest">
                  Email
                </label>
                <input
                  id="fp-email"
                  type="email"
                  required
                  autoFocus
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="you@example.com"
                  className="w-full bg-slate-800/60 border border-slate-600/40 rounded-xl px-4 py-3 font-mono text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 transition-colors"
                />
              </div>

              <button
                type="submit"
                disabled={isPending}
                className="w-full flex items-center justify-center gap-2 bg-blue-500 hover:bg-blue-400 disabled:opacity-60 text-white font-mono text-xs py-3 rounded-xl uppercase tracking-widest transition-colors"
              >
                {isPending ? (
                  <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Sending...</>
                ) : (
                  "Send Reset Link"
                )}
              </button>
            </form>

            <p className="text-center font-mono text-xs text-slate-400">
              Remembered it?{" "}
              <Link href="/login" className="text-blue-400 hover:text-blue-300 transition-colors underline underline-offset-2">
                Sign in
              </Link>
            </p>
          </div>
        </div>
      </main>
    </div>
  );
}
