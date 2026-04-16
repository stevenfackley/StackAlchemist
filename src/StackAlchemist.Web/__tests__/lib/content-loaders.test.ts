import { describe, it, expect } from "vitest";
import { getBlogPostBySlug } from "../../src/lib/blog";
import { getCompareBySlug } from "../../src/lib/compare";
import { getSolutionContentBySlug } from "../../src/lib/solutions";
import { BLOG_POSTS } from "../../src/lib/blog-manifest";
import { COMPARE_ENTRIES } from "../../src/lib/compare-manifest";
import { SOLUTION_ENTRIES } from "../../src/lib/solutions-manifest";

/**
 * Loader tests hit the real file system. During vitest, process.cwd() is
 * src/StackAlchemist.Web, so loaders resolve to ../../content/<type>/*.md.
 * That directory is seeded in the repo, so a clean checkout already has the
 * markdown files these assertions depend on. If the tests fail with null
 * content for a known slug, either demo mode is on, or content files moved.
 */

describe("blog loader", () => {
  it("returns meta + non-empty content for every manifest slug", () => {
    for (const post of BLOG_POSTS) {
      const loaded = getBlogPostBySlug(post.slug);
      expect(loaded, `missing content for ${post.slug}`).not.toBeNull();
      expect(loaded?.meta.slug).toBe(post.slug);
      expect(loaded?.content.length, `empty content for ${post.slug}`).toBeGreaterThan(200);
    }
  });

  it("returns null for unknown slug", () => {
    expect(getBlogPostBySlug("does-not-exist")).toBeNull();
  });

  it("first blog post mentions Steve as the founder byline", () => {
    const loaded = getBlogPostBySlug(BLOG_POSTS[0].slug);
    expect(loaded?.content).toMatch(/Steve/);
  });
});

describe("compare loader", () => {
  it("returns meta + content for every compare entry", () => {
    for (const entry of COMPARE_ENTRIES) {
      const loaded = getCompareBySlug(entry.slug);
      expect(loaded, `missing content for ${entry.slug}`).not.toBeNull();
      expect(loaded?.meta.slug).toBe(entry.slug);
      expect(loaded?.content.length).toBeGreaterThan(200);
    }
  });

  it("returns null for unknown slug", () => {
    expect(getCompareBySlug("made-up-competitor")).toBeNull();
  });
});

describe("solutions loader", () => {
  it("returns meta + content for every solution entry", () => {
    for (const entry of SOLUTION_ENTRIES) {
      const loaded = getSolutionContentBySlug(entry.slug);
      expect(loaded, `missing content for ${entry.slug}`).not.toBeNull();
      expect(loaded?.meta.slug).toBe(entry.slug);
      expect(loaded?.content.length).toBeGreaterThan(200);
    }
  });

  it("returns null for unknown slug", () => {
    expect(getSolutionContentBySlug("unknown-vertical")).toBeNull();
  });
});
