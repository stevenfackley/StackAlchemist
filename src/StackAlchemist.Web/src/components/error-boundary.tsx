"use client";

import { Component, type ReactNode } from "react";
import { AlertCircle, RefreshCw } from "lucide-react";

type Props = {
  /** Short label shown in the fallback so users know what failed. */
  section: string;
  children: ReactNode;
  /** Optional fallback override; falls back to the default UI when omitted. */
  fallback?: (error: Error, reset: () => void) => ReactNode;
};

type State = { error: Error | null };

/**
 * Minimal React error boundary. Use to wrap a logical UI section so a render or
 * runtime error in a child does not blank out the entire page. Catches errors
 * during render, lifecycle, and constructors of descendants — does NOT catch
 * errors in event handlers, async code, or server-rendered output (use try/catch
 * for those).
 */
export class SectionErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error) {
    if (typeof window !== "undefined") {
      console.error(`[SectionErrorBoundary:${this.props.section}]`, error);
    }
  }

  reset = () => this.setState({ error: null });

  render() {
    if (this.state.error) {
      if (this.props.fallback) return this.props.fallback(this.state.error, this.reset);
      return <DefaultFallback section={this.props.section} onReset={this.reset} />;
    }
    return this.props.children;
  }
}

function DefaultFallback({ section, onReset }: { section: string; onReset: () => void }) {
  return (
    <div className="rounded-2xl border border-rose-500/30 bg-rose-500/5 px-6 py-8 text-center space-y-3">
      <div className="mx-auto h-10 w-10 rounded-full bg-rose-500/15 flex items-center justify-center">
        <AlertCircle className="h-5 w-5 text-rose-400" />
      </div>
      <p className="font-mono text-xs text-rose-200 uppercase tracking-widest">
        {section} failed to load
      </p>
      <p className="font-mono text-[11px] text-slate-400 leading-relaxed max-w-sm mx-auto">
        Something on this section threw — try resetting it. Other parts of the page
        are still safe to use.
      </p>
      <button
        type="button"
        onClick={onReset}
        className="inline-flex items-center gap-1.5 rounded-full bg-slate-700/40 hover:bg-slate-700/60 border border-slate-600/40 px-4 py-2 font-mono text-[10px] uppercase tracking-widest text-slate-200 transition-colors"
      >
        <RefreshCw className="h-3 w-3" />
        Retry section
      </button>
    </div>
  );
}
