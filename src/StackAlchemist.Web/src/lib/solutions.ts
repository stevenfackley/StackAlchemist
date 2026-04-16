import fs from "fs";
import path from "path";
import {
  SOLUTION_ENTRIES,
  type SolutionEntry,
  getSolutionBySlug as getMeta,
} from "@/lib/solutions-manifest";
import { isDemoMode } from "./runtime-config";

export type { SolutionEntry };
export { SOLUTION_ENTRIES };

const SOLUTIONS_ROOT = path.join(process.cwd(), "..", "..", "content", "solutions");

export function getSolutionContentBySlug(
  slug: string,
): { meta: SolutionEntry; content: string } | null {
  const meta = getMeta(slug);
  if (!meta) return null;

  try {
    const filePath = path.join(SOLUTIONS_ROOT, meta.relativePath);
    const content = fs.readFileSync(filePath, "utf-8");
    return { meta, content };
  } catch {
    if (isDemoMode) {
      return {
        meta,
        content: `# ${meta.h1}\n\nDemo mode is active, so repo-level solution content is not loaded.`,
      };
    }
    return null;
  }
}
