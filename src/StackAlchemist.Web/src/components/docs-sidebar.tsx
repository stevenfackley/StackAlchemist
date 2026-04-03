"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { DOCS, DOC_SECTIONS } from "@/lib/docs-manifest";
import { BookOpen, Zap, ChevronRight } from "lucide-react";

const SECTION_ICONS = {
  user: BookOpen,
  advanced: Zap,
};

export function DocsSidebar() {
  const pathname = usePathname();

  return (
    <nav className="space-y-8">
      {DOC_SECTIONS.map((section) => {
        const Icon = SECTION_ICONS[section.key];
        const docs = DOCS.filter((d) => d.section === section.key);

        return (
          <div key={section.key}>
            {/* Section label */}
            <div className="flex items-center gap-2 mb-3 px-3">
              <Icon className="h-3.5 w-3.5 text-blue-400" />
              <span className="font-mono text-[10px] tracking-[0.25em] text-blue-400 uppercase">
                {section.label}
              </span>
            </div>

            {/* Doc links */}
            <ul className="space-y-0.5">
              {docs.map((doc) => {
                const href = `/docs/${doc.slug}`;
                const isActive = pathname === href;

                return (
                  <li key={doc.slug}>
                    <Link
                      href={href}
                      className={`
                        group flex items-center justify-between rounded-lg px-3 py-2
                        text-sm transition-all duration-200
                        ${
                          isActive
                            ? "bg-blue-500/15 text-white border-l-2 border-blue-400 pl-2.5"
                            : "text-slate-400 hover:text-slate-200 hover:bg-slate-700/50 border-l-2 border-transparent pl-2.5"
                        }
                      `}
                    >
                      <span className={isActive ? "font-medium" : ""}>{doc.title}</span>
                      {isActive && (
                        <ChevronRight className="h-3 w-3 text-blue-400 shrink-0" />
                      )}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </div>
        );
      })}
    </nav>
  );
}
