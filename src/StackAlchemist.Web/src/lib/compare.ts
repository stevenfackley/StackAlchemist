import fs from "fs";
import path from "path";
import {
  COMPARE_ENTRIES,
  type CompareEntry,
  getCompareEntryBySlug,
} from "@/lib/compare-manifest";
import { isDemoMode } from "./runtime-config";

export type { CompareEntry };
export { COMPARE_ENTRIES };

const COMPARE_ROOT = path.join(process.cwd(), "..", "..", "content", "compare");

export function getCompareBySlug(
  slug: string,
): { meta: CompareEntry; content: string } | null {
  const meta = getCompareEntryBySlug(slug);
  if (!meta) return null;

  try {
    const filePath = path.join(COMPARE_ROOT, meta.relativePath);
    const content = fs.readFileSync(filePath, "utf-8");
    return { meta, content };
  } catch {
    if (isDemoMode) {
      return {
        meta,
        content: `# ${meta.tagline}\n\nDemo mode is active, so repo-level comparison content is not loaded.`,
      };
    }
    return null;
  }
}
