"use client";

import { useActionState } from "react";
import { Cpu, ExternalLink, Key, Save } from "lucide-react";
import { saveProfileSettings } from "@/lib/actions";
import type { ProfileSettings, SaveProfileSettingsState } from "@/lib/types";

const INITIAL_STATE: SaveProfileSettingsState = {
  status: "idle",
  message: "",
};

const MODEL_OPTIONS = [
  {
    value: "claude-3-5-sonnet-20241022",
    label: "Claude 3.5 Sonnet (default)",
  },
  {
    value: "claude-3-5-haiku-20241022",
    label: "Claude 3.5 Haiku",
  },
  {
    value: "openai/gpt-4o-mini",
    label: "OpenAI GPT-4o mini (BYOK)",
  },
  {
    value: "openrouter/anthropic/claude-3.5-sonnet",
    label: "OpenRouter Claude 3.5 Sonnet (BYOK)",
  },
];

export function ByokSettingsForm({ settings }: { settings: ProfileSettings }) {
  const [state, formAction, isPending] = useActionState(saveProfileSettings, INITIAL_STATE);

  return (
    <div className="rounded-2xl border border-slate-600/40 bg-slate-700/10 overflow-hidden">
      <div className="px-6 py-4 border-b border-slate-600/30 bg-slate-700/20 flex items-center gap-3">
        <Key className="h-4 w-4 text-blue-400 shrink-0" />
        <div>
          <h2 className="font-mono text-xs font-bold text-white uppercase tracking-widest">
            API Settings
          </h2>
          <p className="font-mono text-[10px] text-slate-500 mt-0.5">
            Bring Your Own Key (BYOK) and preferred generation model
          </p>
        </div>
      </div>

      <form action={formAction} className="p-6 space-y-5">
        <div className="flex items-center justify-between gap-4">
          <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">
            Account
          </span>
          <span className="font-mono text-xs text-white truncate">{settings.email}</span>
        </div>

        <div className="space-y-1.5">
          <label
            htmlFor="apiKeyOverride"
            className="font-mono text-[10px] text-slate-400 uppercase tracking-widest flex items-center gap-1.5"
          >
            API Key Override
            <span className="text-slate-600 normal-case tracking-normal">(optional)</span>
          </label>
          <input
            id="apiKeyOverride"
            name="apiKeyOverride"
            type="password"
            autoComplete="off"
            placeholder={settings.hasApiKeyOverride ? "Saved key on file - enter a new key to replace" : "sk-ant-..."}
            className="w-full bg-slate-800/60 border border-slate-600/40 rounded-xl px-4 py-3 font-mono text-sm text-slate-200 placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 focus:ring-2 focus:ring-blue-500/10"
          />
          <p className="font-mono text-[10px] text-slate-600 leading-relaxed">
            Keys are AES-GCM encrypted before storage. Leave blank to keep the existing key.
          </p>
        </div>

        {settings.hasApiKeyOverride && (
          <label className="flex items-center gap-2 font-mono text-[10px] text-slate-400 uppercase tracking-widest">
            <input
              type="checkbox"
              name="clearApiKey"
              value="true"
              className="h-3.5 w-3.5 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500/30"
            />
            Clear saved API key
          </label>
        )}

        <div className="space-y-1.5">
          <label
            htmlFor="preferredModel"
            className="font-mono text-[10px] text-slate-400 uppercase tracking-widest flex items-center gap-1.5"
          >
            <Cpu className="h-3 w-3" />
            Preferred Model
          </label>
          <select
            id="preferredModel"
            name="preferredModel"
            defaultValue={settings.preferredModel}
            className="w-full bg-slate-800/60 border border-slate-600/40 rounded-xl px-4 py-3 font-mono text-sm text-slate-200 focus:outline-none focus:border-blue-500/60 focus:ring-2 focus:ring-blue-500/10"
          >
            {MODEL_OPTIONS.map((model) => (
              <option key={model.value} value={model.value}>
                {model.label}
              </option>
            ))}
          </select>
          <p className="font-mono text-[10px] text-slate-600">
            Saved now for generation routing; Engine-side provider routing is a follow-up change.
          </p>
        </div>

        {state.message && (
          <p
            className={`font-mono text-[10px] leading-relaxed ${
              state.status === "success" ? "text-emerald-400" : "text-rose-400"
            }`}
          >
            {state.message}
          </p>
        )}

        <div className="pt-2 border-t border-slate-700/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <a
            href="https://docs.anthropic.com/en/api/getting-started"
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 font-mono text-[10px] text-blue-400 hover:text-blue-300 transition-colors uppercase tracking-widest"
          >
            <ExternalLink className="h-3 w-3" />
            Anthropic API Docs
          </a>

          <button
            type="submit"
            disabled={isPending}
            className="inline-flex items-center justify-center gap-2 rounded-full bg-blue-500 hover:bg-blue-400 disabled:bg-slate-700 disabled:text-slate-500 text-white px-5 py-2.5 font-mono text-[10px] uppercase tracking-widest transition-colors"
          >
            <Save className="h-3.5 w-3.5" />
            {isPending ? "Saving" : "Save Settings"}
          </button>
        </div>
      </form>
    </div>
  );
}
