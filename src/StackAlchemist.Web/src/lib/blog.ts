import fs from "fs";
import path from "path";
import {
  BLOG_POSTS,
  type BlogPostMeta,
  getBlogPostMetaBySlug,
} from "@/lib/blog-manifest";
import { isDemoMode } from "./runtime-config";

export type { BlogPostMeta };
export { BLOG_POSTS };

const BLOG_ROOT = path.join(process.cwd(), "..", "..", "content", "blog");

function resolveFilePath(relativePath: string): string {
  return path.join(BLOG_ROOT, relativePath);
}

export function getBlogPostBySlug(
  slug: string,
): { meta: BlogPostMeta; content: string } | null {
  const meta = getBlogPostMetaBySlug(slug);
  if (!meta) return null;

  try {
    const filePath = resolveFilePath(meta.relativePath);
    const content = fs.readFileSync(filePath, "utf-8");
    return { meta, content };
  } catch {
    if (isDemoMode) {
      return {
        meta,
        content: `# ${meta.title}\n\nDemo mode is active, so repo-level blog content is not loaded.\n\nTo see the full post, run the app from the full repository with the /content folder available.`,
      };
    }
    return null;
  }
}
