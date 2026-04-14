"use client";

import { useState } from "react";
import { X, ChevronRight, ChevronLeft, Zap, Smile, Briefcase, Flame, Feather, Sparkles, Shield, Users, ShoppingBag, Stethoscope, GraduationCap, Building2, HeartHandshake, Baby, Leaf, Coffee, Layers, LayoutGrid, Settings2 } from "lucide-react";
import { cn } from "@/lib/utils";
import {
  COLOR_PALETTES,
  DEFAULT_PERSONALIZATION,
  type PersonalizationData,
  type ColorPalette,
  type AppVibe,
  type AppComplexity,
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
      <div className="flex rounded-md overflow-hidden h-6">
        <div className="flex-1" style={{ background: palette.background }} />
        <div className="flex-1" style={{ background: palette.surface }} />
        <div className="flex-1" style={{ background: palette.primary }} />
        <div className="flex-1" style={{ background: palette.accent }} />
      </div>
      <p className="font-mono text-[11px] font-medium text-white">{palette.name}</p>
      {selected && <p className="font-mono text-[10px] text-blue-400">Selected</p>}
    </button>
  );
}

// ─── Step 1: Your Big Idea ────────────────────────────────────────────────────
function StepBigIdea({
  data,
  onChange,
}: {
  data: PersonalizationData;
  onChange: (patch: Partial<PersonalizationData>) => void;
}) {
  const [useHelper, setUseHelper] = useState(false);
  const [who, setWho] = useState("");
  const [doWhat, setDoWhat] = useState("");
  const [outcome, setOutcome] = useState("");

  function applyMadLib() {
    if (who && doWhat) {
      const sentence = outcome
        ? `My app helps ${who} to ${doWhat} so they can ${outcome}.`
        : `My app helps ${who} to ${doWhat}.`;
      onChange({ businessDescription: sentence });
      setUseHelper(false);
    }
  }

  return (
    <div className="space-y-5">
      <p className="text-slate-300 text-sm leading-relaxed">
        Tell us about your idea. Don&apos;t worry about technical details — just describe it like you would to a friend.
      </p>

      <div className="space-y-2">
        <label className="font-mono text-xs text-slate-500 uppercase tracking-widest">What&apos;s your project called?</label>
        <input
          value={data.projectName ?? ""}
          onChange={(e) => onChange({ projectName: e.target.value })}
          placeholder="e.g. FoodHub, MyGym, PetPal"
          className="w-full bg-slate-800/60 border border-slate-600/40 rounded-lg px-3 py-2 text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
        />
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <label className="font-mono text-xs text-slate-500 uppercase tracking-widest">Describe your idea</label>
          <button
            type="button"
            onClick={() => setUseHelper(!useHelper)}
            className="font-mono text-[10px] text-blue-400 hover:text-blue-300 transition-colors tracking-widest uppercase"
          >
            {useHelper ? "Write it myself" : "✦ Help me write this"}
          </button>
        </div>

        {useHelper ? (
          <div className="rounded-xl border border-blue-500/20 bg-blue-500/5 p-4 space-y-3">
            <p className="text-xs text-slate-400">Fill in the blanks:</p>
            <div className="space-y-2 text-sm text-white">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-slate-400 shrink-0">My app helps</span>
                <input
                  value={who}
                  onChange={(e) => setWho(e.target.value)}
                  placeholder="gym owners"
                  className="flex-1 min-w-[100px] bg-slate-700/60 border border-slate-600/40 rounded px-2 py-1 text-xs text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
                />
              </div>
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-slate-400 shrink-0">to</span>
                <input
                  value={doWhat}
                  onChange={(e) => setDoWhat(e.target.value)}
                  placeholder="manage members and billing"
                  className="flex-1 min-w-[140px] bg-slate-700/60 border border-slate-600/40 rounded px-2 py-1 text-xs text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
                />
              </div>
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-slate-400 shrink-0">so they can</span>
                <input
                  value={outcome}
                  onChange={(e) => setOutcome(e.target.value)}
                  placeholder="spend less time on admin (optional)"
                  className="flex-1 min-w-[140px] bg-slate-700/60 border border-slate-600/40 rounded px-2 py-1 text-xs text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
                />
              </div>
            </div>
            <button
              type="button"
              onClick={applyMadLib}
              disabled={!who || !doWhat}
              className="font-mono text-[10px] bg-blue-500 hover:bg-blue-400 disabled:opacity-40 text-white px-3 py-1.5 rounded-full uppercase tracking-widest transition-colors"
            >
              Use this description
            </button>
          </div>
        ) : (
          <textarea
            value={data.businessDescription}
            onChange={(e) => onChange({ businessDescription: e.target.value })}
            rows={3}
            placeholder="e.g. An app for gym owners to track memberships, check-ins, and payments without needing a spreadsheet."
            className="w-full bg-slate-800/60 border border-slate-600/40 rounded-lg px-3 py-2 text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 resize-none"
          />
        )}

        {data.businessDescription && (
          <p className="text-xs text-slate-400 bg-slate-700/30 rounded-lg px-3 py-2 border border-slate-600/20">
            <span className="text-slate-500 font-mono text-[10px] uppercase tracking-widest">Preview: </span>
            {data.businessDescription}
          </p>
        )}
      </div>

      <div className="space-y-2">
        <label className="font-mono text-xs text-slate-500 uppercase tracking-widest">Tagline <span className="text-slate-600 normal-case">(optional)</span></label>
        <input
          value={data.tagline ?? ""}
          onChange={(e) => onChange({ tagline: e.target.value })}
          placeholder="e.g. Run your gym. Not your inbox."
          className="w-full bg-slate-800/60 border border-slate-600/40 rounded-lg px-3 py-2 text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
        />
      </div>
    </div>
  );
}

// ─── Step 2: App Vibe ─────────────────────────────────────────────────────────
const VIBES: { id: AppVibe; label: string; desc: string; icon: React.ElementType; emoji: string }[] = [
  { id: "friendly",     label: "Friendly & Warm",       desc: "Approachable, casual, feels like a helpful friend",   icon: Smile,        emoji: "😊" },
  { id: "professional", label: "Professional & Clean",   desc: "Polished, trustworthy, serious about business",       icon: Briefcase,    emoji: "💼" },
  { id: "bold",         label: "Bold & Energetic",       desc: "High-contrast, makes a statement, stands out",        icon: Flame,        emoji: "🔥" },
  { id: "minimal",      label: "Minimal & Calm",         desc: "Less is more — clean, breathing room, no clutter",    icon: Feather,      emoji: "🌿" },
  { id: "playful",      label: "Playful & Fun",          desc: "Bright, expressive, brings a smile to users' faces",  icon: Sparkles,     emoji: "✨" },
  { id: "trustworthy",  label: "Safe & Trustworthy",     desc: "Security-first, calm colors, instills confidence",    icon: Shield,       emoji: "🛡️" },
];

const AUDIENCE_OPTIONS = [
  { id: "consumers",    label: "Everyday people",     icon: Users },
  { id: "businesses",   label: "Business teams",      icon: Building2 },
  { id: "shoppers",     label: "Shoppers & buyers",   icon: ShoppingBag },
  { id: "health",       label: "Health & wellness",   icon: Stethoscope },
  { id: "students",     label: "Students & learners", icon: GraduationCap },
  { id: "volunteers",   label: "Nonprofits & causes", icon: HeartHandshake },
  { id: "parents",      label: "Parents & families",  icon: Baby },
  { id: "eco",          label: "Eco & sustainability",icon: Leaf },
  { id: "hospitality",  label: "Food & hospitality",  icon: Coffee },
];

function StepVibeAndAudience({
  data,
  onChange,
}: {
  data: PersonalizationData;
  onChange: (patch: Partial<PersonalizationData>) => void;
}) {
  return (
    <div className="space-y-6">
      {/* Vibe */}
      <div className="space-y-3">
        <div>
          <p className="font-mono text-xs text-slate-500 uppercase tracking-widest mb-1">What&apos;s the vibe?</p>
          <p className="text-slate-400 text-xs">Pick the personality that best fits your app.</p>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          {VIBES.map((v) => {
            const Icon = v.icon;
            const selected = data.appVibe === v.id;
            return (
              <button
                key={v.id}
                type="button"
                onClick={() => onChange({ appVibe: v.id })}
                className={cn(
                  "flex items-start gap-3 rounded-xl border p-3 text-left transition-all duration-150",
                  selected
                    ? "border-blue-500/70 bg-blue-500/10 shadow-[0_0_12px_rgba(59,130,246,0.12)]"
                    : "border-slate-600/30 bg-slate-700/20 hover:border-slate-500/50"
                )}
              >
                <span className="text-xl shrink-0 mt-0.5">{v.emoji}</span>
                <div>
                  <p className={cn("text-xs font-semibold", selected ? "text-blue-300" : "text-white")}>{v.label}</p>
                  <p className="text-[11px] text-slate-500 mt-0.5 leading-relaxed">{v.desc}</p>
                </div>
                {selected && <Icon className="h-3.5 w-3.5 text-blue-400 shrink-0 ml-auto mt-0.5" />}
              </button>
            );
          })}
        </div>
      </div>

      {/* Audience */}
      <div className="space-y-3">
        <div>
          <p className="font-mono text-xs text-slate-500 uppercase tracking-widest mb-1">Who is this for?</p>
          <p className="text-slate-400 text-xs">Choose the audience your app is built around.</p>
        </div>
        <div className="grid grid-cols-3 gap-2">
          {AUDIENCE_OPTIONS.map((a) => {
            const Icon = a.icon;
            const selected = data.targetAudience === a.id;
            return (
              <button
                key={a.id}
                type="button"
                onClick={() => onChange({ targetAudience: a.id })}
                className={cn(
                  "flex flex-col items-center gap-1.5 rounded-xl border p-3 text-center transition-all duration-150",
                  selected
                    ? "border-blue-500/70 bg-blue-500/10"
                    : "border-slate-600/30 bg-slate-700/20 hover:border-slate-500/50"
                )}
              >
                <Icon className={cn("h-4 w-4", selected ? "text-blue-400" : "text-slate-400")} />
                <p className={cn("text-[10px] leading-tight", selected ? "text-blue-300" : "text-slate-300")}>{a.label}</p>
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}

// ─── Step 3: Look & Feel ──────────────────────────────────────────────────────

// Theme sets — quick bundles of vibe + palette
const THEME_SETS: { id: string; label: string; emoji: string; desc: string; vibe: AppVibe; paletteId: string }[] = [
  { id: "trust",    label: "Corporate Trust",   emoji: "🏛️", desc: "Blue, professional, confidence-first",       vibe: "trustworthy",  paletteId: "corporate-blue" },
  { id: "energy",   label: "Bold & Energetic",  emoji: "⚡", desc: "Orange fire, stands out, gets attention",    vibe: "bold",         paletteId: "warm-startup" },
  { id: "hacker",   label: "Dev / Builder",     emoji: "💻", desc: "Terminal green, minimal, developer-focused", vibe: "minimal",      paletteId: "dark-hacker" },
  { id: "earthy",   label: "Warm & Natural",    emoji: "🌻", desc: "Amber tones, earthy, grounded and calm",     vibe: "friendly",     paletteId: "earthy-minimal" },
  { id: "premium",  label: "Premium SaaS",      emoji: "💜", desc: "Deep purple, luxe, high-end product feel",   vibe: "professional", paletteId: "bold-saas" },
  { id: "aqua",     label: "Fresh & Modern",    emoji: "🌊", desc: "Electric teal, clean, forward-thinking",     vibe: "playful",      paletteId: "electric-teal" },
];

function StepColorScheme({
  data,
  onChange,
}: {
  data: PersonalizationData;
  onChange: (patch: Partial<PersonalizationData>) => void;
}) {
  function applyThemeSet(set: typeof THEME_SETS[number]) {
    const palette = COLOR_PALETTES.find((p) => p.id === set.paletteId) ?? COLOR_PALETTES[0];
    onChange({ colorScheme: palette, appVibe: set.vibe });
  }

  return (
    <div className="space-y-5">
      <p className="text-slate-300 text-sm leading-relaxed">
        Pick a color palette — or grab a quick theme set that bundles colors and vibe together.
      </p>

      {/* Quick theme sets */}
      <div className="space-y-2">
        <p className="font-mono text-[10px] text-slate-500 uppercase tracking-widest flex items-center gap-1.5">
          <Sparkles className="h-3 w-3" /> Quick theme sets
        </p>
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
          {THEME_SETS.map((set) => {
            const palette = COLOR_PALETTES.find((p) => p.id === set.paletteId);
            const isActive = data.colorScheme.id === set.paletteId && data.appVibe === set.vibe;
            return (
              <button
                key={set.id}
                type="button"
                onClick={() => applyThemeSet(set)}
                className={cn(
                  "flex flex-col gap-2 rounded-xl border p-3 text-left transition-all duration-150",
                  isActive
                    ? "border-blue-500/70 bg-blue-500/10"
                    : "border-slate-600/30 bg-slate-700/20 hover:border-slate-500/50"
                )}
              >
                {/* Mini color strip */}
                {palette && (
                  <div className="flex rounded overflow-hidden h-3">
                    <div className="flex-1" style={{ background: palette.background }} />
                    <div className="flex-1" style={{ background: palette.surface }} />
                    <div className="flex-1" style={{ background: palette.primary }} />
                    <div className="flex-1" style={{ background: palette.accent }} />
                  </div>
                )}
                <div className="flex items-center gap-1.5">
                  <span className="text-sm">{set.emoji}</span>
                  <span className={cn("text-[11px] font-semibold", isActive ? "text-blue-300" : "text-white")}>{set.label}</span>
                </div>
                <p className="text-[10px] text-slate-500 leading-relaxed">{set.desc}</p>
              </button>
            );
          })}
        </div>
      </div>

      <div className="flex items-center gap-3">
        <div className="flex-1 h-px bg-slate-700/50" />
        <p className="text-[10px] text-slate-600 uppercase tracking-widest">or pick manually</p>
        <div className="flex-1 h-px bg-slate-700/50" />
      </div>

      {/* Manual palette */}
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

      <div className="rounded-xl border border-slate-600/30 overflow-hidden">
        <div className="px-3 py-2 flex items-center gap-2 border-b border-slate-600/30"
          style={{ background: data.colorScheme.surface }}>
          <div className="h-2 w-2 rounded-full" style={{ background: data.colorScheme.primary }} />
          <span className="font-mono text-[11px] text-slate-300">Preview — {data.colorScheme.name}</span>
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
        </div>
      </div>
    </div>
  );
}

// ─── Step 4: Name Your Things ─────────────────────────────────────────────────
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
      <p className="text-slate-400 text-sm">No entities found yet — describe your idea in step 1 first.</p>
    );
  }

  const friendlyLabels: Record<string, string> = {
    User: "your main users",
    Plan: "your pricing plans",
    Subscription: "a subscription",
    CheckIn: "a check-in event",
    Product: "a product",
    Order: "an order",
    Customer: "a customer",
    Category: "a category",
    Review: "a review",
    Payment: "a payment",
  };

  return (
    <div className="space-y-4">
      <p className="text-slate-300 text-sm leading-relaxed">
        Help us understand what each piece of your app represents — in plain words, not tech speak.
      </p>
      <div className="space-y-4">
        {entities.map((name) => (
          <div key={name} className="space-y-1.5">
            <label className="text-sm font-medium text-white">
              What is a <span className="text-blue-400">{name}</span> in your app?
            </label>
            <input
              value={data.domainContext[name] ?? ""}
              onChange={(e) => onChange({
                domainContext: { ...data.domainContext, [name]: e.target.value }
              })}
              placeholder={`e.g. ${friendlyLabels[name] ?? `a record in your app`} — describe it simply`}
              className="w-full bg-slate-800/60 border border-slate-600/40 rounded-lg px-3 py-2 text-sm text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
            />
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Step 5: Inspiration & Complexity ────────────────────────────────────────
const INSPIRATION_APPS = [
  { id: "airbnb",    label: "Airbnb",    emoji: "🏠" },
  { id: "notion",    label: "Notion",    emoji: "📝" },
  { id: "stripe",    label: "Stripe",    emoji: "💳" },
  { id: "slack",     label: "Slack",     emoji: "💬" },
  { id: "shopify",   label: "Shopify",   emoji: "🛍️" },
  { id: "uber",      label: "Uber",      emoji: "🚗" },
  { id: "duolingo",  label: "Duolingo",  emoji: "🦜" },
  { id: "figma",     label: "Figma",     emoji: "🎨" },
  { id: "calendly",  label: "Calendly",  emoji: "📅" },
  { id: "discord",   label: "Discord",   emoji: "🎮" },
  { id: "spotify",   label: "Spotify",   emoji: "🎵" },
  { id: "trello",    label: "Trello",    emoji: "📋" },
  { id: "instagram", label: "Instagram", emoji: "📸" },
  { id: "linkedin",  label: "LinkedIn",  emoji: "🤝" },
  { id: "mailchimp", label: "Mailchimp", emoji: "📧" },
  { id: "typeform",  label: "Typeform",  emoji: "📊" },
];

const COMPLEXITY_OPTIONS: { id: AppComplexity; label: string; desc: string; hint: string; icon: React.ElementType; emoji: string }[] = [
  {
    id: "simple",
    label: "Just the basics",
    desc: "A focused app with one or two core features",
    hint: "Perfect for MVP, side project, or proof of concept",
    icon: Feather,
    emoji: "🌱",
  },
  {
    id: "standard",
    label: "Standard app",
    desc: "Multiple features, user accounts, and some workflows",
    hint: "Good for most startups and small business apps",
    icon: LayoutGrid,
    emoji: "🏗️",
  },
  {
    id: "full",
    label: "Full-featured platform",
    desc: "Rich feature set, roles, integrations, dashboards",
    hint: "When you need it all from day one",
    icon: Layers,
    emoji: "🚀",
  },
];

function StepInspirationAndComplexity({
  data,
  onChange,
}: {
  data: PersonalizationData;
  onChange: (patch: Partial<PersonalizationData>) => void;
}) {
  const selected = data.inspirationApps ?? [];

  function toggleApp(id: string) {
    if (selected.includes(id)) {
      onChange({ inspirationApps: selected.filter((a) => a !== id) });
    } else if (selected.length < 5) {
      onChange({ inspirationApps: [...selected, id] });
    }
  }

  return (
    <div className="space-y-6">
      {/* Inspiration */}
      <div className="space-y-3">
        <div>
          <p className="font-mono text-xs text-slate-500 uppercase tracking-widest mb-1">Apps that inspire you</p>
          <p className="text-slate-400 text-xs">Pick up to 5. This helps set the tone and UX feel of the generated app.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          {INSPIRATION_APPS.map((app) => {
            const isSelected = selected.includes(app.id);
            const maxReached = selected.length >= 5 && !isSelected;
            return (
              <button
                key={app.id}
                type="button"
                onClick={() => toggleApp(app.id)}
                disabled={maxReached}
                className={cn(
                  "flex items-center gap-1.5 px-3 py-1.5 rounded-full border text-xs transition-all duration-150",
                  isSelected
                    ? "border-blue-500/70 bg-blue-500/15 text-blue-300"
                    : maxReached
                    ? "border-slate-700/30 bg-slate-800/20 text-slate-600 cursor-not-allowed"
                    : "border-slate-600/30 bg-slate-700/20 text-slate-300 hover:border-slate-500/50 hover:text-white"
                )}
              >
                <span>{app.emoji}</span>
                <span>{app.label}</span>
              </button>
            );
          })}
        </div>
        {selected.length > 0 && (
          <p className="text-[11px] text-slate-500">{selected.length}/5 selected</p>
        )}
      </div>

      {/* Complexity */}
      <div className="space-y-3">
        <div>
          <p className="font-mono text-xs text-slate-500 uppercase tracking-widest mb-1">How big is your app?</p>
          <p className="text-slate-400 text-xs">This shapes how much gets generated and how it&apos;s structured.</p>
        </div>
        <div className="space-y-2">
          {COMPLEXITY_OPTIONS.map((opt) => {
            const Icon = opt.icon;
            const isSelected = data.appComplexity === opt.id;
            return (
              <button
                key={opt.id}
                type="button"
                onClick={() => onChange({ appComplexity: opt.id })}
                className={cn(
                  "w-full flex items-start gap-4 rounded-xl border p-4 text-left transition-all duration-150",
                  isSelected
                    ? "border-blue-500/70 bg-blue-500/10 shadow-[0_0_12px_rgba(59,130,246,0.1)]"
                    : "border-slate-600/30 bg-slate-700/20 hover:border-slate-500/50"
                )}
              >
                <span className="text-2xl shrink-0">{opt.emoji}</span>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <p className={cn("text-sm font-semibold", isSelected ? "text-blue-300" : "text-white")}>{opt.label}</p>
                    {isSelected && <Icon className="h-3.5 w-3.5 text-blue-400" />}
                  </div>
                  <p className="text-xs text-slate-400 mt-0.5">{opt.desc}</p>
                  <p className="text-[11px] text-slate-500 mt-1 italic">{opt.hint}</p>
                </div>
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}

// ─── Step: A Few Preferences ─────────────────────────────────────────────────
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

  const LOGIN_OPTIONS: { id: PersonalizationData["featureFlags"]["authMethod"]; label: string; desc: string; emoji: string }[] = [
    { id: "jwt",    label: "Email & Password",    desc: "Classic login with email + password",          emoji: "📧" },
    { id: "cookie", label: "Stay Logged In",      desc: "Browser remembers your session automatically", emoji: "🍪" },
    { id: "oauth",  label: "Sign in with Google", desc: "Login via Google, GitHub, or other accounts",  emoji: "🔑" },
    { id: "none",   label: "No Login Needed",     desc: "Public app — no accounts required",            emoji: "🌐" },
  ];

  const toggles: { key: keyof Omit<PersonalizationData["featureFlags"], "authMethod">; label: string; desc: string; emoji: string }[] = [
    { key: "softDelete",           label: "Keep deleted items",   desc: "Deleted records stay hidden, not permanently removed", emoji: "🗂️" },
    { key: "auditTimestamps",      label: "Track when things change", desc: "Automatically log when records are created or updated", emoji: "🕐" },
    { key: "includeSwagger",       label: "API docs",             desc: "Auto-generate a reference page for your app's API",    emoji: "📖" },
    { key: "includeDockerCompose", label: "Easy local setup",     desc: "One-command local development environment",            emoji: "🐳" },
  ];

  return (
    <div className="space-y-5">
      <p className="text-slate-300 text-sm leading-relaxed">
        A few quick preferences. These shape how your app works under the hood — but we&apos;ve written them in plain terms.
      </p>

      <div className="space-y-2">
        <p className="font-mono text-xs text-slate-500 uppercase tracking-widest">How do users log in?</p>
        <div className="grid grid-cols-2 gap-2">
          {LOGIN_OPTIONS.map((opt) => (
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
              <p className="text-base mb-1">{opt.emoji}</p>
              <p className={cn("text-xs font-semibold", flags.authMethod === opt.id ? "text-blue-400" : "text-white")}>{opt.label}</p>
              <p className="text-[10px] text-slate-500 mt-0.5 leading-relaxed">{opt.desc}</p>
            </button>
          ))}
        </div>
      </div>

      <div className="space-y-2">
        <p className="font-mono text-xs text-slate-500 uppercase tracking-widest">Nice-to-haves</p>
        <div className="space-y-2">
          {toggles.map(({ key, label, desc, emoji }) => (
            <button
              key={key}
              type="button"
              onClick={() => patchFlags({ [key]: !flags[key] })}
              className="w-full flex items-center justify-between rounded-lg border border-slate-600/30 bg-slate-700/20 hover:border-slate-500/40 px-4 py-3 transition-colors"
            >
              <div className="text-left flex items-start gap-3">
                <span className="text-base shrink-0">{emoji}</span>
                <div>
                  <p className="text-sm text-white">{label}</p>
                  <p className="text-[11px] text-slate-500 mt-0.5">{desc}</p>
                </div>
              </div>
              <div className={cn(
                "w-9 h-5 rounded-full flex items-center transition-colors shrink-0 ml-3",
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
  { title: "Your Idea",            subtitle: "Name and describe your project" },
  { title: "Vibe & Audience",      subtitle: "Personality and who it's for" },
  { title: "Look & Feel",          subtitle: "Color palette and theme" },
  { title: "Inspiration & Scale",  subtitle: "Apps you love and how big this is" },
  { title: "Name Your Things",     subtitle: "What each part of your app means" },
  { title: "A Few Preferences",    subtitle: "Login style and optional features" },
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

  return (
    <div className="fixed inset-0 z-[200] flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
      <div className="w-full max-w-xl bg-slate-800 border border-slate-600/50 rounded-2xl shadow-2xl flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-700/50 px-6 py-4 shrink-0">
          <div>
            <div className="flex items-center gap-2">
              <Zap className="h-4 w-4 text-blue-400" />
              <h2 className="font-mono text-sm font-bold text-white tracking-widest uppercase">Make It Yours</h2>
            </div>
            <p className="text-xs text-slate-500 mt-0.5">
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

        {/* Step dots */}
        <div className="flex items-center justify-center gap-2 px-6 py-3 border-b border-slate-700/50 shrink-0">
          {SUB_STEPS.map((s, i) => (
            <button
              key={s.title}
              onClick={() => setSubStep(i + 1)}
              title={s.title}
              className={cn(
                "rounded-full transition-all duration-200",
                subStep === i + 1
                  ? "w-6 h-2 bg-blue-500"
                  : subStep > i + 1
                  ? "w-2 h-2 bg-emerald-500/70"
                  : "w-2 h-2 bg-slate-600 hover:bg-slate-500"
              )}
            />
          ))}
          <span className="ml-2 text-xs text-slate-500">{SUB_STEPS[subStep - 1].title}</span>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto px-6 py-5">
          {subStep === 1 && <StepBigIdea data={data} onChange={patchData} />}
          {subStep === 2 && <StepVibeAndAudience data={data} onChange={patchData} />}
          {subStep === 3 && <StepColorScheme data={data} onChange={patchData} />}
          {subStep === 4 && <StepInspirationAndComplexity data={data} onChange={patchData} />}
          {subStep === 5 && <StepDomainVocabulary entities={entityNames} data={data} onChange={patchData} />}
          {subStep === 6 && <StepFeatureFlags data={data} onChange={patchData} />}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between border-t border-slate-700/50 px-6 py-4 shrink-0">
          <button
            onClick={onSkip}
            className="text-xs text-slate-500 hover:text-slate-300 tracking-widest uppercase transition-colors"
          >
            Skip &rarr; Use defaults
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
                onClick={() => onComplete(data)}
                className="font-mono text-xs bg-emerald-500 hover:bg-emerald-400 text-white px-4 py-1.5 rounded-full uppercase tracking-widest transition-colors flex items-center gap-1.5"
              >
                Build It <ChevronRight className="h-3 w-3" />
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
