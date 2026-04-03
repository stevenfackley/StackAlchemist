import fs from "fs";
import path from "path";
import { DOCS, type DocMeta } from "@/lib/docs-manifest";

export type { DocMeta };
export { DOCS };

// Docs live at repo root /docs/ — CWD is src/StackAlchemist.Web when running next dev/build
const DOCS_ROOT = path.join(process.cwd(), "..", "..", "docs");

function resolveFilePath(relativePath: string): string {
  return path.join(DOCS_ROOT, relativePath);
}

export function getDocBySlug(slug: string): { meta: DocMeta; content: string } | null {
  const meta = DOCS.find((d) => d.slug === slug);
  if (!meta) return null;

  try {
    const filePath = resolveFilePath(meta.relativePath);
    const content = fs.readFileSync(filePath, "utf-8");
    return { meta, content };
  } catch {
    return null;
  }
}

export function getAllSlugs(): string[] {
  return DOCS.map((d) => d.slug);
}
