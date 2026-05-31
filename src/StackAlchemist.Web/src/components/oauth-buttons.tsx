"use client";

import { useState } from "react";
import { Loader2, AlertCircle } from "lucide-react";
import { supabase } from "@/lib/supabase";

type Provider = "google" | "apple";

/**
 * Social sign-in buttons shared by the login and register pages.
 *
 * `signInWithOAuth` performs a full-page redirect to the provider, which returns
 * to `/auth/callback?code=…` — the same PKCE handler the magic-link and email
 * confirmation flows use — so the server sets the session cookie and the navbar
 * renders the authed state. No soft-nav cookie race here.
 *
 * Providers must be enabled in Supabase → Authentication → Providers first;
 * until then a click surfaces the provider's "not enabled" error inline.
 */
export function OAuthButtons({ returnTo }: { returnTo: string }) {
  const [loading, setLoading] = useState<Provider | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function signIn(provider: Provider) {
    setError(null);
    setLoading(provider);

    if (!supabase) {
      setError("Social sign-in is unavailable right now.");
      setLoading(null);
      return;
    }

    const { error: oauthError } = await supabase.auth.signInWithOAuth({
      provider,
      options: {
        redirectTo: `${window.location.origin}/auth/callback?next=${encodeURIComponent(returnTo)}`,
      },
    });

    // On success the browser is already navigating to the provider — keep the
    // spinner up. Only an error returns control to us here.
    if (oauthError) {
      setError(oauthError.message);
      setLoading(null);
    }
  }

  return (
    <div className="space-y-3">
      {error && (
        <div className="flex items-start gap-2.5 rounded-xl border border-rose-500/30 bg-rose-500/10 px-4 py-3">
          <AlertCircle className="h-4 w-4 text-rose-400 shrink-0 mt-0.5" />
          <p className="font-mono text-xs text-rose-300">{error}</p>
        </div>
      )}

      <button
        type="button"
        onClick={() => signIn("google")}
        disabled={loading !== null}
        className="w-full flex items-center justify-center gap-2.5 bg-white hover:bg-slate-100 disabled:opacity-60 text-slate-800 font-mono text-xs py-3 rounded-xl uppercase tracking-widest transition-colors"
      >
        {loading === "google" ? (
          <Loader2 className="h-3.5 w-3.5 animate-spin" />
        ) : (
          <GoogleIcon />
        )}
        Continue with Google
      </button>

      <button
        type="button"
        onClick={() => signIn("apple")}
        disabled={loading !== null}
        className="w-full flex items-center justify-center gap-2.5 bg-black hover:bg-slate-900 disabled:opacity-60 text-white font-mono text-xs py-3 rounded-xl uppercase tracking-widest transition-colors border border-slate-700"
      >
        {loading === "apple" ? (
          <Loader2 className="h-3.5 w-3.5 animate-spin" />
        ) : (
          <AppleIcon />
        )}
        Continue with Apple
      </button>
    </div>
  );
}

function GoogleIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" aria-hidden="true">
      <path
        fill="#4285F4"
        d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
      />
      <path
        fill="#34A853"
        d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
      />
      <path
        fill="#FBBC05"
        d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"
      />
      <path
        fill="#EA4335"
        d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"
      />
    </svg>
  );
}

function AppleIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M17.05 12.04c-.03-2.6 2.13-3.85 2.22-3.91-1.21-1.77-3.09-2.01-3.76-2.04-1.6-.16-3.12.94-3.93.94-.81 0-2.06-.92-3.39-.89-1.74.03-3.35 1.01-4.25 2.57-1.81 3.14-.46 7.79 1.3 10.34.86 1.25 1.89 2.65 3.24 2.6 1.3-.05 1.79-.84 3.36-.84 1.57 0 2.01.84 3.39.81 1.4-.02 2.29-1.27 3.14-2.53.99-1.45 1.4-2.86 1.42-2.93-.03-.01-2.73-1.05-2.76-4.16zM14.46 4.5c.72-.87 1.2-2.08 1.07-3.28-1.03.04-2.28.69-3.02 1.56-.66.77-1.24 2-1.08 3.18 1.15.09 2.32-.59 3.03-1.46z" />
    </svg>
  );
}
