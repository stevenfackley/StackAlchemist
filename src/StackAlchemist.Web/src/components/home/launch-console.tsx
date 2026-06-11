"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Terminal, Sparkles } from "lucide-react";
import { AlchemyInput } from "@/components/alchemy-input";
import { Alert } from "@/components/ui";
import { useFreeQuota } from "@/lib/hooks/use-free-quota";
import { useLocalStorageDraft } from "@/lib/hooks/use-local-storage-draft";

export function LaunchConsole() {
  const router = useRouter();
  const [mode, setMode] = useState<"simple" | "advanced">("simple");
  // Drafted to localStorage (silent restore — it's a visible text box) so an
  // accidental refresh or auth bounce doesn't eat a carefully written prompt.
  // Deliberately NOT cleared on submit: the failure/back-navigation path is
  // exactly when the user wants it back.
  const { value: prompt, setValue: setPrompt } = useLocalStorageDraft(
    "sa:draft:home-prompt:v1",
    "",
    { version: 1, isDefault: (v) => v.trim() === "" },
  );
  // Deferred client fetch keeps this SEO landing page statically prerendered.
  const { quota } = useFreeQuota();

  function handleSubmit() {
    if (mode === "advanced") {
      router.push("/advanced");
      return;
    }
    if (!prompt.trim()) return;
    router.push(`/simple?q=${encodeURIComponent(prompt.trim())}`);
  }

  return (
    <section data-testid="home-launch-console" className="relative z-10 border-t border-slate-600/30 px-6 py-16 sm:px-8 sm:py-20 lg:px-16">
      <div className="mx-auto max-w-7xl">
        <div className="mb-8 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-2xl">
            <div className="font-mono text-xs tracking-[0.28em] text-blue-400 uppercase">Launch Console</div>
            <h2 className="mt-3 text-2xl font-semibold text-white sm:text-3xl">
              Describe your product. Get a buildable architecture.
            </h2>
          </div>
          <div className="max-w-md text-sm leading-relaxed text-slate-400 lg:text-right">
            Start in plain language, or switch to the entity wizard when you already know your schema. Tap the
            builder chips to assemble a complete brief in seconds.
          </div>
        </div>

        <div className="rounded-[28px] border border-slate-600/30 bg-slate-800/60 p-4 shadow-[0_0_40px_rgba(15,23,42,0.35)] backdrop-blur-md sm:p-6 lg:p-7">
          <div className="mb-5 flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <div className="font-mono text-xs tracking-[0.28em] text-blue-400 uppercase">Workspace Mode</div>
              <h3 className="mt-2 text-xl font-semibold text-white sm:text-2xl">
                Move from idea to architecture without leaving the main flow.
              </h3>
            </div>
            <div className="text-xs text-slate-500 font-mono sm:max-w-[220px] sm:text-right">
              {mode === "simple" ? "Prompt-first synthesis path" : "Structured wizard path"}
            </div>
          </div>

          <div className="mb-5 grid gap-3 rounded-2xl border border-slate-700/40 bg-slate-900/35 px-4 py-4 md:grid-cols-[minmax(0,1fr)_auto] md:items-center">
            <div>
              <div className="text-sm font-semibold text-white">Choose how you want to start</div>
              <p className="mt-1 text-xs leading-relaxed text-slate-400 sm:text-sm">
                Use plain language when the brief is still fuzzy, or switch to the wizard when you already know
                your entities, relationships, and API shape.
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2 md:justify-end">
              <button
                onClick={() => setMode("simple")}
                data-testid="home-mode-simple-button"
                className={`rounded-full px-4 py-2 text-sm font-medium transition-all duration-300 ${
                  mode === "simple"
                    ? "bg-blue-500 text-white shadow-[0_0_20px_rgba(59,130,246,0.3)]"
                    : "bg-slate-700/50 text-slate-400 hover:text-slate-200"
                }`}
              >
                Simple Mode
              </button>
              <button
                onClick={() => setMode("advanced")}
                data-testid="home-mode-advanced-button"
                className={`rounded-full px-4 py-2 text-sm font-medium transition-all duration-300 ${
                  mode === "advanced"
                    ? "bg-blue-500 text-white shadow-[0_0_20px_rgba(59,130,246,0.3)]"
                    : "bg-slate-700/50 text-slate-400 hover:text-slate-200"
                }`}
              >
                Advanced Mode
              </button>
            </div>
          </div>

          <div className="mb-5 text-xs text-slate-500 font-mono">
            {mode === "simple" ? "Natural language prompt" : "Visual entity wizard"}
          </div>

          {mode === "simple" ? (
            <>
              {quota?.remaining === 0 && (
                <Alert variant="warning" className="mb-3" data-testid="home-quota-exhausted">
                  You&apos;ve used all {quota.limit} free builds this month. Resets {quota.resetsAtLabel}.{" "}
                  <a href="#pricing" className="underline underline-offset-2">
                    View paid tiers →
                  </a>
                </Alert>
              )}
              <AlchemyInput
                value={prompt}
                onChange={setPrompt}
                onSubmit={handleSubmit}
                disabled={quota?.remaining === 0}
                className="max-w-none"
              />
              {quota && quota.remaining > 0 && (
                <div className="mt-3 flex items-center gap-2 text-xs text-slate-400">
                  <Sparkles className="h-3.5 w-3.5 text-emerald-400" />
                  <span>
                    <span className="font-semibold text-emerald-400">
                      {quota.remaining} of {quota.limit}
                    </span>{" "}
                    free builds left this month
                  </span>
                </div>
              )}
              <div className="mt-4 grid gap-3 lg:grid-cols-[1.1fr_0.9fr]">
                <div className="rounded-2xl border border-slate-600/30 bg-slate-900/40 px-4 py-4">
                  <div className="font-mono text-[11px] uppercase tracking-[0.24em] text-blue-400">
                    What You Walk Away With
                  </div>
                  <div className="mt-3 grid gap-3 sm:grid-cols-3">
                    <div>
                      <div className="text-sm font-semibold text-white">Blueprint</div>
                      <div className="mt-1 text-xs leading-relaxed text-slate-400">
                        Schema, API surface, and planning artifacts.
                      </div>
                    </div>
                    <div>
                      <div className="text-sm font-semibold text-white">Boilerplate</div>
                      <div className="mt-1 text-xs leading-relaxed text-slate-400">
                        Generated .NET and Next.js application structure.
                      </div>
                    </div>
                    <div>
                      <div className="text-sm font-semibold text-white">Infrastructure</div>
                      <div className="mt-1 text-xs leading-relaxed text-slate-400">
                        IaC, charts, and deployment guidance.
                      </div>
                    </div>
                  </div>
                </div>
                <div className="rounded-2xl border border-slate-600/30 bg-slate-900/40 px-4 py-4">
                  <div className="font-mono text-[11px] uppercase tracking-[0.24em] text-blue-400">
                    Flow
                  </div>
                  <div className="mt-3 space-y-2">
                    {[
                      "1. Define the idea or model the entities",
                      "2. Review the architecture shape",
                      "3. Choose the handoff depth",
                    ].map((step) => (
                      <div key={step} className="text-xs leading-relaxed text-slate-400">
                        {step}
                      </div>
                    ))}
                  </div>
                </div>
              </div>
              <div className="mt-4 flex flex-col gap-3 border-t border-slate-700/40 pt-4 text-xs text-slate-500 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-center gap-2">
                  <span>Press</span>
                  <kbd className="rounded border border-slate-500/40 bg-slate-700/50 px-2 py-0.5 font-mono text-xs text-slate-300">
                    Ctrl + Enter
                  </kbd>
                  <span>to synthesize</span>
                </div>
                <div className="font-mono uppercase tracking-[0.2em] text-slate-600">
                  Prompt first, details second
                </div>
              </div>
            </>
          ) : (
            <div className="rounded-2xl border border-blue-500/35 bg-slate-700/60 p-6 backdrop-blur-md">
              <div className="mb-3 flex items-center gap-3">
                <Terminal className="h-5 w-5 text-blue-400" />
                <span className="font-mono text-xs tracking-wider text-slate-400 uppercase">Advanced Mode Active</span>
              </div>
              <p className="mb-5 text-sm leading-relaxed text-slate-300 sm:text-base">
                Use the visual entity wizard to define your schema with precision across entities,
                relationships, API endpoints, and tier selection without leaving the main flow.
              </p>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="text-xs uppercase tracking-[0.2em] text-slate-500">
                  Opens the full entity wizard
                </div>
                <button
                  onClick={handleSubmit}
                  className="rounded-full bg-blue-500 px-6 py-2.5 text-sm font-medium text-white transition-all duration-300 hover:-translate-y-0.5 hover:bg-blue-400 hover:shadow-[0_0_24px_rgba(59,130,246,0.45)]"
                >
                  Open Entity Wizard →
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
