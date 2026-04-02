"use client";

import { useState, useRef, useEffect } from "react";
import { Sparkles, Send, Terminal, Layers, Zap } from "lucide-react";
import { cn } from "@/lib/utils";

interface AlchemyInputProps {
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  disabled?: boolean;
  className?: string;
}

export function AlchemyInput({ value, onChange, onSubmit, disabled, className }: AlchemyInputProps) {
  const [isFocused, setIsFocused] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto";
      textareaRef.current.style.height = `${Math.min(textareaRef.current.scrollHeight, 200)}px`;
    }
  }, [value]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if ((e.metaKey || e.ctrlKey) && e.key === "Enter") {
      e.preventDefault();
      onSubmit();
    }
  };

  return (
    <div className={cn("relative w-full max-w-2xl", className)}>
      {/* Outer glow */}
      <div
        className={`absolute -inset-0.5 rounded-2xl bg-gradient-to-r from-blue-500/20 via-transparent to-blue-500/10 opacity-0 blur-xl transition-opacity duration-500 ${
          isFocused ? "opacity-100" : ""
        }`}
      />

      {/* Main container */}
      <div
        className={`relative overflow-hidden rounded-2xl border backdrop-blur-md transition-all duration-300 ${
          isFocused
            ? "border-blue-500/35 bg-slate-700/80 shadow-[0_0_30px_rgba(59,130,246,0.2)]"
            : "border-slate-500/25 bg-slate-700/60 hover:border-slate-500/40"
        }`}
      >
        {/* Terminal header bar */}
        <div className="flex items-center justify-between border-b border-slate-500/25 px-4 py-3">
          <div className="flex items-center gap-3">
            <Terminal className="h-4 w-4 text-blue-400" />
            <span className="font-mono text-xs tracking-wider text-slate-400">SYNTHESIS PROMPT</span>
          </div>
          <div className="flex items-center gap-2">
            <div className="h-2 w-2 rounded-full bg-slate-600" />
            <div className="h-2 w-2 rounded-full bg-slate-600" />
            <div
              className={`h-2 w-2 rounded-full transition-colors duration-300 ${
                isFocused ? "bg-blue-500 shadow-[0_0_8px_rgba(59,130,246,0.6)]" : "bg-slate-600"
              }`}
            />
          </div>
        </div>

        {/* Input area */}
        <div className="relative p-4">
          <textarea
            ref={textareaRef}
            value={value}
            onChange={(e) => onChange(e.target.value)}
            onFocus={() => setIsFocused(true)}
            onBlur={() => setIsFocused(false)}
            onKeyDown={handleKeyDown}
            disabled={disabled}
            placeholder="Describe your architecture... e.g., 'A real-time collaborative document editor with CRDT sync, PostgreSQL persistence, and team workspaces'"
            className="min-h-[100px] w-full resize-none bg-transparent font-mono text-sm leading-relaxed text-white placeholder:text-slate-500 focus:outline-none disabled:opacity-40"
            rows={3}
          />
        </div>

        {/* Action bar */}
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 border-t border-slate-500/25 px-4 py-3">
          {/* Quick action chips */}
          <div className="flex items-center gap-2 flex-wrap">
            <button
              type="button"
              onClick={() => onChange(value + (value ? " " : "") + "microservices architecture")}
              className="flex items-center gap-1.5 rounded-full border border-slate-500/35 bg-slate-600/50 px-3 py-1.5 text-xs text-slate-300 transition-all duration-300 hover:-translate-y-0.5 hover:border-blue-500/40 hover:bg-slate-600/70 hover:text-white"
            >
              <Layers className="h-3 w-3" />
              <span>Microservices</span>
            </button>
            <button
              type="button"
              onClick={() => onChange(value + (value ? " " : "") + "with serverless functions")}
              className="flex items-center gap-1.5 rounded-full border border-slate-500/35 bg-slate-600/50 px-3 py-1.5 text-xs text-slate-300 transition-all duration-300 hover:-translate-y-0.5 hover:border-blue-500/40 hover:bg-slate-600/70 hover:text-white"
            >
              <Zap className="h-3 w-3" />
              <span>Serverless</span>
            </button>
          </div>

          {/* Submit button */}
          <button
            type="button"
            onClick={onSubmit}
            disabled={!value.trim() || disabled}
            className={`group flex items-center gap-2 rounded-full px-5 py-2 font-medium text-sm transition-all duration-300 whitespace-nowrap ${
              value.trim() && !disabled
                ? "bg-blue-500 text-white hover:-translate-y-0.5 hover:bg-blue-400 hover:shadow-[0_0_24px_rgba(59,130,246,0.45)]"
                : "bg-slate-600 text-slate-400 cursor-not-allowed"
            }`}
          >
            <Sparkles
              className={`h-4 w-4 transition-transform duration-300 ${
                value.trim() ? "group-hover:rotate-12" : ""
              }`}
            />
            <span>Synthesize</span>
            <Send
              className={`h-3.5 w-3.5 transition-transform duration-300 ${
                value.trim() ? "group-hover:translate-x-0.5" : ""
              }`}
            />
          </button>
        </div>

        {/* Scan line effect when focused */}
        <div
          className={`pointer-events-none absolute inset-0 opacity-0 transition-opacity duration-300 ${
            isFocused ? "opacity-100" : ""
          }`}
          style={{
            backgroundImage:
              "linear-gradient(transparent 50%, rgba(59, 130, 246, 0.015) 50%)",
            backgroundSize: "100% 4px",
            backgroundRepeat: "repeat",
          }}
        />
      </div>
    </div>
  );
}
