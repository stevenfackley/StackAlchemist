import Link from "next/link";
import { Alert, Button } from "@/components/ui";
import { Logo } from "@/components/logo";
import { authErrorMessage } from "@/lib/auth-errors";
import { safeReturnTo } from "@/lib/proxy-utils";
import { getQavrenAuthUrl } from "@/lib/runtime-config";
import { loginAction } from "./actions";

/**
 * Qavren Auth sign-in: one button that hands off to the realm's own screen,
 * which owns password entry, Google and password reset. Only the closed set of
 * error codes renders copy — never the `error` parameter itself.
 */
export function QavrenLoginPage({ params }: { params: { error?: string; returnTo?: string } }) {
  const message = authErrorMessage(params.error);
  const returnTo = safeReturnTo(params.returnTo);
  const authHost = new URL(getQavrenAuthUrl()).host;
  const registerHref = `/register${returnTo !== "/" ? `?returnTo=${encodeURIComponent(returnTo)}` : ""}`;

  return (
    <div className="min-h-screen flex flex-col bg-slate-800">
      <header className="border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0 z-header">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center gap-4">
          <Logo variant="mono" size={28} />
        </div>
      </header>

      <main className="flex-1 flex items-center justify-center px-4 py-16">
        <div className="w-full max-w-md space-y-6">
          {message && (
            <Alert variant="error" data-testid="login-auth-error">
              {message}
            </Alert>
          )}

          <div className="text-center space-y-2">
            <h1 className="text-2xl font-bold text-white tracking-tight">Sign in</h1>
            <p className="text-slate-400 text-sm">Track your generations and access your downloads.</p>
          </div>

          <div className="rounded-2xl border border-slate-600/40 bg-slate-700/20 p-8 space-y-5">
            <p className="text-slate-400 text-sm leading-relaxed">
              Sign-in happens on the StackAlchemist sign-in service at{" "}
              <strong className="text-white font-medium">{authHost}</strong>. The address bar changes to that
              name while you enter your password or continue with Google, then brings you back here.
            </p>

            <form action={loginAction}>
              <input type="hidden" name="returnTo" value={returnTo} />
              <Button type="submit" className="w-full rounded-xl">
                Sign in
              </Button>
            </form>

            <p className="text-center font-mono text-xs text-slate-500">
              Forgot your password? Reset it on the sign-in screen.
            </p>
            <p className="text-center font-mono text-xs text-slate-400">
              New here?{" "}
              <Link
                href={registerHref}
                className="text-accent hover:text-accent/80 transition-colors underline underline-offset-2"
              >
                Create an account
              </Link>
            </p>
          </div>
        </div>
      </main>
    </div>
  );
}
