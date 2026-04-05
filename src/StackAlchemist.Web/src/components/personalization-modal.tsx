"use client";

import { useState } from "react";
import { X, ChevronRight, ChevronLeft, Zap } from "lucide-react";
import { cn } from "@/lib/utils";
import {
  COLOR_PALETTES,
  DEFAULT_PERSONALIZATION,
  type PersonalizationData,
  type ColorPalette,
} from "@/lib/types";

// ─── Palette Swatch ───────────────────────────────────────────────────────────
function PaletteSwatch({ palette, selected, onClick }: {
  palette: ColorPalette;
  selected: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "rounded-xl border p-3 text-left transition-all duration-200 space-y-2",
        selected
          ? "border-blue-500/70 bg-blue-500/10 shadow-[0_0_16px_rgba(59,130,246,0.15)]"
          : "border-slate-600/30 bg-slate-700/20 hover:border-slate-500/50"
      )}
    >
      {/* Swatch strip */}
      <div className="flex rounded-md overflow-hidden h-6">
        <div className="flex-1" style={{ background: palette.background }} />
        <div className="flex-1" style={{ background: palette.surface }} />
        <div className="flex-1" style={{ background: palette.primary }} />
        <div className="flex-1" style={{ background: palette.accent }} />
      </div>
      <p className="font-mono text-[11px] font-medium text-white">{palette.name}</p>
      {selected && (
        <p className="font-mono text-[10px] text-blue-400">Selected</p>
      )}
    </button>
  );
}

// ─── Sub-step components ─────────────────────────────────────────────────────

function StepBusiness({
  data,
  onChange,
}: {
  data: PersonalizationData;
  onChange: (patch: Partial<PersonalizationData>) => void;
}) {
  return (
    <div className="space-y-5">
      <p className="text-slate-400 text-sm">
        Tell us about your project so the generated code reflects your brand, domain language, and business context.
      </p>
      <div className="space-y-2">
        <label className="font-mono text-xs text-slate-500 uppercase tracking-widest">Project / Company Name</label>
        <input
          value={data.projectName ?? ""}
          onChange={(e) => onChange({ projectName: e.target.value })}
          placeholder="e.g. AcmeCRM, FoodHub, GymManager"
          className="w-full bg-slate-800/60 border border-slate-600/40 rounded-lg px-3 py-2 font-mono text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
        />
      </div>
      <div className="space-y-2">
        <label className="font-mono text-xs text-slate-500 uppercase tracking-widest">Business Description</label>
        <textarea
          value={data.businessDescription}
          onChange={(e) => onChange({ businessDescription: e.target.value })}
          rows={3}
          placeholder="2-3 sentences: industry, target audience, core value proposition. E.g. 'A B2B SaaS for gym chains that automates member check-in and subscription billing.'"
          className="w-full bg-slate-800/60 border border-slate-600/40 rounded-lg px-3 py-2 font-mono text-xs text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 resize-none"
        />
      </div>
      <div className="space-y-2">
        <label className="font-mono text-xs text-slate-500 uppercase tracking-widest">Tagline <span className="text-slate-600 normal-case">(optional)</span></label>
        <input
          value={data.tagline ?? ""}
          onChange={(e) => onChange({ tagline: e.target.value })}
          placeholder="e.g. 'Ship faster, bill smarter'"
          className="w-full bg-slate-800/60 border border-slate-600/40 rounded-lg px-3 py-2 font-mono text-xs text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
        />
      </div>
    </div>
  );
}

function StepColorScheme({
  data,
  onChange,
}: {
  data: PersonalizationData;
  onChange: (patch: Partial<PersonalizationData>) => void;
}) {
  return (
    <div className="space-y-4">
      <p className="text-slate-400 text-sm">
        Choose a color theme. This sets Tailwind CSS tokens in the generated project&apos;s config.
      </p>
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {COLOR_PALETTES.map((p) => (
          <PaletteSwatch
            key={p.id}
            palette={p}
            selected={data.colorScheme.id === p.id}
            onClick={() => onChange({ colorScheme: p })}
          />
        ))}
      </div>
      {/* Live preview strip */}
      <div className="rounded-xl border border-slate-600/30 overflow-hidden">
        <div className="px-3 py-2 flex items-center gap-2 border-b border-slate-600/30"
          style={{ background: data.colorScheme.surface }}>
          <div className="h-2 w-2 rounded-full" style={{ background: data.colorScheme.primary }} />
          <span className="font-mono text-[11px] text-slate-300">Live preview — {data.colorScheme.name}</span>
        </div>
        <div className="p-4 flex flex-col gap-2" style={{ background: data.colorScheme.background }}>
          <div className="h-7 rounded px-3 flex items-center w-32"
            style={{ background: data.colorScheme.primary }}>
            <span className="font-mono text-[10px] text-white">Primary Button</span>
          </div>
          <div className="h-7 rounded px-3 flex items-center w-28 border"
            style={{ background: data.colorScheme.surface, borderColor: data.colorScheme.primary + "60" }}>
            <span className="font-mono text-[10px]" style={{ color: data.colorScheme.accent }}>Accent Text</span>
          </div>
          <p className="font-mono text-[10px] text-slate-400">bg: {data.colorScheme.background} · primary: {data.colorScheme.primary}</p>
        </div>
      </div>
    </div>
  );
}

function StepDomainVocabulary({
  entities,
  data,
  onChange,
}: {
  entities: string[];
  data: PersonalizationData;
  onChange: (patch: Partial<PersonalizationData>) => void;
}) {
  if (entities.length === 0) {
    return (
      <p className="text-slate-400 text-sm">No entities defined yet — add entities in step 1 first.</p>
    );
  }

  return (
    <div className="space-y-4">
      <p className="text-slate-400 text-sm">
        Describe what each entity means in your business. This enriches generated code with realistic domain language.
      </p>
      <div className="space-y-4">
        {entities.map((name) => (
          <div key={name} className="space-y-1.5">
            <label className="font-mono text-xs text-blue-400 uppercase tracking-widest">{name}</label>
            <input
              value={data.domainContext[name] ?? ""}
              onChange={(e) => onChange({
                domainContext: { ...data.domainContext, [name]: e.target.value }
              })}
              placeholder={`What does ${name} represent? e.g. "A gym member with an active subscription"`}
              className="w-full bg-slate-800/60 border border-slate-600/40 rounded-lg px-3 py-2 font-mono text-xs text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
            />
          </div>
        ))}
      </div>
    </div>
  );
}

function StepFeatureFlags({
  data,
  onChange,
}: {
  data: PersonalizationData;
  onChange: (patch: Partial<PersonalizationData>) => void;
}) {
  const flags = data.featureFlags;

  function patchFlags(patch: Partial<PersonalizationData["featureFlags"]>) {
    onChange({ featureFlags: { ...flags, ...patch } });
  }

  const AUTH_OPTIONS: { id: PersonalizationData["featureFlags"]["authMethod"]; label: string; desc: string }[] = [
    { id: "jwt",    label: "JWT",          desc: "Stateless bearer tokens" },
    { id: "cookie", label: "Cookie",       desc: "HTTP-only session cookies" },
    { id: "oauth",  label: "OAuth/OIDC",   desc: "Third-party provider login" },
    { id: "none",   label: "None",         desc: "No auth scaffolding" },
  ];

  const toggles: { key: keyof Omit<PersonalizationData["featureFlags"], "authMethod">; label: string; desc: string }[] = [
    { key: "softDelete",        label: "Soft Delete",        desc: "Add deleted_at column instead of hard deletes" },
    { key: "auditTimestamps",   label: "Audit Timestamps",   desc: "created_at + updated_at on every table" },
    { key: "includeSwagger",    label: "Swagger / OpenAPI",  desc: "Auto-generated API documentation endpoint" },
    { key: "includeDockerCompose", label: "Docker Compose",  desc: "Local dev orchestration file" },
  ];

  return (
    <div className="space-y-5">
      <p className="text-slate-400 text-sm">
        Optional feature toggles that map directly to Handlebars flags in the generated templates.
      </p>
      <div className="space-y-2">
        <p className="font-mono text-xs text-slate-500 uppercase tracking-widest">Authentication Method</p>
        <div className="grid grid-cols-2 gap-2">
          {AUTH_OPTIONS.map((opt) => (
            <button
              key={opt.id}
              type="button"
              onClick={() => patchFlags({ authMethod: opt.id })}
              className={cn(
                "rounded-lg border p-3 text-left transition-all",
                flags.authMethod === opt.id
                  ? "border-blue-500/60 bg-blue-500/10"
                  : "border-slate-600/30 bg-slate-700/20 hover:border-slate-500/50"
              )}
            >
              <p className={cn("font-mono text-xs font-bold", flags.authMethod === opt.id ? "text-blue-400" : "text-white")}>{opt.label}</p>
              <p className="font-mono text-[10px] text-slate-500 mt-0.5">{opt.desc}</p>
            </button>
          ))}
        </div>
      </div>

      <div className="space-y-2">
        <p className="font-mono text-xs text-slate-500 uppercase tracking-widest">Feature Toggles</p>
        <div className="space-y-2">
          {toggles.map(({ key, label, desc }) => (
            <button
              key={key}
              type="button"
              onClick={() => patchFlags({ [key]: !flags[key] })}
              className="w-full flex items-center justify-between rounded-lg border border-slate-600/30 bg-slate-700/20 hover:border-slate-500/40 px-4 py-3 transition-colors"
            >
              <div className="text-left">
                <p className="font-mono text-xs text-white">{label}</p>
                <p className="font-mono text-[10px] text-slate-500">{desc}</p>
              </div>
              <div className={cn(
                "w-9 h-5 rounded-full flex items-center transition-colors shrink-0",
                flags[key] ? "bg-blue-500" : "bg-slate-600"
              )}>
                <div className={cn(
                  "w-3.5 h-3.5 rounded-full bg-white shadow transition-transform mx-0.5",
                  flags[key] ? "translate-x-4" : "translate-x-0"
                )} />
              </div>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

// ─── Main Modal ───────────────────────────────────────────────────────────────
const SUB_STEPS = [
  { title: "Business Identity",     subtitle: "Name, description, and tagline" },
  { title: "Color Scheme",          subtitle: "Tailwind theme for generated UI" },
  { title: "Domain Vocabulary",     subtitle: "What each entity means in your business" },
  { title: "Feature Preferences",   subtitle: "Auth, soft-delete, Swagger, Docker" },
];

export function PersonalizationModal({
  entityNames,
  onComplete,
  onSkip,
}: {
  entityNames: string[];
  onComplete: (data: PersonalizationData) => void;
  onSkip: () => void;
}) {
  const [subStep, setSubStep] = useState(1);
  const [data, setData] = useState<PersonalizationData>(() => ({
    ...DEFAULT_PERSONALIZATION,
    colorScheme: COLOR_PALETTES[0],
  }));

  function patchData(patch: Partial<PersonalizationData>) {
    setData((d) => ({ ...d, ...patch }));
  }

  function handleComplete() {
    onComplete(data);
  }

  return (
    <div className="fixed inset-0 z-[200] flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
      <div className="w-full max-w-xl bg-slate-800 border border-slate-600/50 rounded-2xl shadow-2xl flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-700/50 px-6 py-4 shrink-0">
          <div>
            <div className="flex items-center gap-2">
              <Zap className="h-4 w-4 text-blue-400" />
              <h2 className="font-mono text-sm font-bold text-white tracking-widest uppercase">Personalize Your Project</h2>
            </div>
            <p className="font-mono text-[10px] text-slate-500 mt-0.5 tracking-widest uppercase">
              Step {subStep} of {SUB_STEPS.length} — {SUB_STEPS[subStep - 1].subtitle}
            </p>
          </div>
          <button onClick={onSkip} className="text-slate-500 hover:text-slate-300 transition-colors" title="Skip personalization">
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Progress bar */}
        <div className="h-1 bg-slate-700 shrink-0">
          <div
            className="h-full bg-blue-500 transition-all duration-300"
            style={{ width: `${(subStep / SUB_STEPS.length) * 100}%` }}
          />
        </div>

        {/* Sub-step nav tabs */}
        <div className="flex border-b border-slate-700/50 px-6 shrink-0 overflow-x-auto">
          {SUB_STEPS.map((s, i) => (
            <button
              key={s.title}
              onClick={() => setSubStep(i + 1)}
              className={cn(
                "font-mono text-[10px] tracking-widest uppercase px-3 py-2.5 border-b-2 -mb-px whitespace-nowrap transition-colors",
                subStep === i + 1
                  ? "border-blue-400 text-blue-400"
                  : subStep > i + 1
                  ? "border-emerald-500/40 text-emerald-400/70"
                  : "border-transparent text-slate-500 hover:text-slate-400"
              )}
            >
              {i + 1}. {s.title}
            </button>
          ))}
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto px-6 py-5">
          <h3 className="font-mono text-sm font-bold text-white mb-4">{SUB_STEPS[subStep - 1].title}</h3>
          {subStep === 1 && <StepBusiness data={data} onChange={patchData} />}
          {subStep === 2 && <StepColorScheme data={data} onChange={patchData} />}
          {subStep === 3 && <StepDomainVocabulary entities={entityNames} data={data} onChange={patchData} />}
          {subStep === 4 && <StepFeatureFlags data={data} onChange={patchData} />}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between border-t border-slate-700/50 px-6 py-4 shrink-0">
          <button
            onClick={onSkip}
            className="font-mono text-xs text-slate-500 hover:text-slate-300 tracking-widest uppercase transition-colors"
          >
            Skip All &rarr; Use Defaults
          </button>
          <div className="flex items-center gap-2">
            {subStep > 1 && (
              <button
                onClick={() => setSubStep((s) => s - 1)}
                className="font-mono text-xs border border-slate-600/50 text-slate-400 hover:border-blue-500/40 hover:text-blue-400 px-3 py-1.5 rounded-full uppercase tracking-widest transition-colors flex items-center gap-1"
              >
                <ChevronLeft className="h-3 w-3" /> Back
              </button>
            )}
            {subStep < SUB_STEPS.length ? (
              <button
                onClick={() => setSubStep((s) => s + 1)}
                className="font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white px-4 py-1.5 rounded-full uppercase tracking-widest transition-colors flex items-center gap-1"
              >
                Next <ChevronRight className="h-3 w-3" />
              </button>
            ) : (
              <button
                onClick={handleComplete}
                className="font-mono text-xs bg-emerald-500 hover:bg-emerald-400 text-white px-4 py-1.5 rounded-full uppercase tracking-widest transition-colors flex items-center gap-1.5"
              >
                Apply &amp; Continue <ChevronRight className="h-3 w-3" />
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
