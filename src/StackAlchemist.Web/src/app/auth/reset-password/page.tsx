"use client";

import { useEffect, useState, useTransition } from "react";
import Link from "next/link";
import Image from "next/image";
import { AlertCircle, Loader2 } from "lucide-react";
import { supabase } from "@/lib/supabase";

type Phase = "loading" | "form" | "expired";

export default function ResetPasswordPage() {
  const [phase, setPhase] = useState<Phase>("loading");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  useEffect(() => {
    if (!supabase) { setPhase("expired"); return; }

    // Check cookies set by /auth/callback after code exchange
    supabase.auth.getSession().then(({ data }) => {
      if (data.session) setPhase("form");
    });

    // PASSWORD_RECOVERY fires when the client detects a recovery session
    const { data: { subscription } } = supabase.auth.onAuthStateChange((event, session) => {
      if ((event === "PASSWORD_RECOVERY" || event === "SIGNED_IN") && session) {
        setPhase("form");
      }
    });

    // Fallback: if no session arrives within 4s the link is expired or was already used
    const timer = setTimeout(() => {
      setPhase((prev) => (prev === "loading" ? "expired" : prev));
    }, 4000);

    return () => {
      subscription.unsubscribe();
      clearTimeout(timer);
    };
  }, []);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (password !== confirm) { setErrorMsg("Passwords do not match."); return; }
    if (password.length < 8) { setErrorMsg("Password must be at least 8 characters."); return; }
    startTransition(async () => {
      setErrorMsg(null);
      const { error } = await supabase!.auth.updateUser({ password });
      if (error) {
        setErrorMsg(error.message);
      } else {
        window.location.assign("/");
      }
    });
  }

  const sharedHeader = (
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

  if (phase === "loading") {
    return (
      <div className="min-h-screen flex flex-col bg-slate-800">
        {sharedHeader}
        <main className="flex-1 flex items-center justify-center">
          <Loader2 className="h-8 w-8 text-slate-400 animate-spin" />
        </main>
      </div>
    );
  }

  if (phase === "expired") {
    return (
      <div className="min-h-screen flex flex-col bg-slate-800">
        {sharedHeader}
        <main className="flex-1 flex items-center justify-center px-4 py-16">
          <div className="w-full max-w-md text-center space-y-6">
            <div className="h-16 w-16 rounded-full bg-rose-500/10 border border-rose-500/30 flex items-center justify-center mx-auto">
              <AlertCircle className="h-8 w-8 text-rose-400" />
            </div>
            <div>
              <h2 className="text-2xl font-bold text-white mb-2">Link expired</h2>
              <p className="text-slate-400 text-sm leading-relaxed">
                This reset link is no longer valid. Links expire after 1 hour.
              </p>
            </div>
            <Link
              href="/forgot-password"
              className="inline-block font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white px-6 py-3 rounded-xl uppercase tracking-widest transition-colors"
            >
              Request New Link
            </Link>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-slate-800">
      {sharedHeader}
      <main className="flex-1 flex items-center justify-center px-4 py-16">
        <div className="w-full max-w-md space-y-6">
          <div className="text-center space-y-2">
            <h2 className="text-2xl font-bold text-white tracking-tight">Set New Password</h2>
            <p className="text-slate-400 text-sm">Choose a strong password for your account.</p>
          </div>

          <div className="rounded-2xl border border-slate-600/40 bg-slate-700/20 p-8 space-y-5">
            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="space-y-1.5">
                <label htmlFor="rp-password" className="font-mono text-xs text-slate-400 uppercase tracking-widest">
                  New Password
                </label>
                <input
                  id="rp-password"
                  type="password"
                  required
                  autoFocus
                  minLength={8}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Min. 8 characters"
                  className="w-full bg-slate-800/60 border border-slate-600/40 rounded-xl px-4 py-3 font-mono text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 transition-colors"
                />
              </div>

              <div className="space-y-1.5">
                <label htmlFor="rp-confirm" className="font-mono text-xs text-slate-400 uppercase tracking-widest">
                  Confirm Password
                </label>
                <input
                  id="rp-confirm"
                  type="password"
                  required
                  value={confirm}
                  onChange={(e) => setConfirm(e.target.value)}
                  placeholder="••••••••"
                  className="w-full bg-slate-800/60 border border-slate-600/40 rounded-xl px-4 py-3 font-mono text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 transition-colors"
                />
              </div>

              {errorMsg && (
                <div className="flex items-start gap-2.5 rounded-xl border border-rose-500/30 bg-rose-500/10 px-4 py-3">
                  <AlertCircle className="h-4 w-4 text-rose-400 shrink-0 mt-0.5" aria-hidden />
                  <p className="font-mono text-xs text-rose-300">{errorMsg}</p>
                </div>
              )}

              <button
                type="submit"
                disabled={isPending}
                className="w-full flex items-center justify-center gap-2 bg-blue-500 hover:bg-blue-400 disabled:opacity-60 text-white font-mono text-xs py-3 rounded-xl uppercase tracking-widest transition-colors"
              >
                {isPending ? (
                  <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Updating...</>
                ) : (
                  "Set New Password"
                )}
              </button>
            </form>
          </div>
        </div>
      </main>
    </div>
  );
}
