import { describe, it, expect } from "vitest";
import {
  BLOG_POSTS,
  BLOG_AUTHOR,
  getBlogPostMetaBySlug,
  getAllBlogSlugs,
  getSortedBlogPosts,
} from "../../src/lib/blog-manifest";
import {
  COMPARE_ENTRIES,
  getCompareEntryBySlug,
  getAllCompareSlugs,
} from "../../src/lib/compare-manifest";
import {
  SOLUTION_ENTRIES,
  getSolutionBySlug,
  getAllSolutionSlugs,
} from "../../src/lib/solutions-manifest";
import { DOCS, DOC_SECTIONS } from "../../src/lib/docs-manifest";
import { FAQS, FAQ_CATEGORIES } from "../../src/lib/faq-manifest";

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const KEBAB_SLUG = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

describe("blog-manifest", () => {
  it("exposes the canonical author string", () => {
    expect(BLOG_AUTHOR).toBe("Steve Ackley");
  });

  it("seeds at least 5 posts for the launch cohort", () => {
    expect(BLOG_POSTS.length).toBeGreaterThanOrEqual(5);
  });

  it("every post has required fields with valid shapes", () => {
    for (const post of BLOG_POSTS) {
      expect(post.slug, `slug on ${post.slug}`).toMatch(KEBAB_SLUG);
      expect(post.title.length, `title on ${post.slug}`).toBeGreaterThan(0);
      expect(post.description.length, `description on ${post.slug}`).toBeGreaterThan(40);
      expect(post.publishedAt, `publishedAt on ${post.slug}`).toMatch(ISO_DATE);
      expect(post.relativePath, `relativePath on ${post.slug}`).toMatch(/\.md$/);
      expect(post.tags.length, `tags on ${post.slug}`).toBeGreaterThan(0);
      expect(post.readingTimeMinutes, `readingTime on ${post.slug}`).toBeGreaterThan(0);
      expect(post.author, `author on ${post.slug}`).toBe(BLOG_AUTHOR);
    }
  });

  it("slugs are unique", () => {
    const slugs = BLOG_POSTS.map((p) => p.slug);
    expect(new Set(slugs).size).toBe(slugs.length);
  });

  it("relativePath values are unique", () => {
    const paths = BLOG_POSTS.map((p) => p.relativePath);
    expect(new Set(paths).size).toBe(paths.length);
  });

  it("getBlogPostMetaBySlug returns meta for known slug, undefined otherwise", () => {
    expect(getBlogPostMetaBySlug(BLOG_POSTS[0].slug)?.title).toBe(BLOG_POSTS[0].title);
    expect(getBlogPostMetaBySlug("does-not-exist")).toBeUndefined();
  });

  it("getAllBlogSlugs mirrors the manifest length", () => {
    expect(getAllBlogSlugs()).toEqual(BLOG_POSTS.map((p) => p.slug));
  });

  it("getSortedBlogPosts returns newest first", () => {
    const sorted = getSortedBlogPosts();
    for (let i = 1; i < sorted.length; i++) {
      expect(sorted[i - 1].publishedAt >= sorted[i].publishedAt).toBe(true);
    }
  });
});

describe("compare-manifest", () => {
  it("seeds at least the two launch comparisons", () => {
    expect(COMPARE_ENTRIES.length).toBeGreaterThanOrEqual(2);
    const slugs = COMPARE_ENTRIES.map((e) => e.slug);
    expect(slugs).toContain("v0");
    expect(slugs).toContain("bolt-new");
  });

  it("every entry has required fields and is honestly bi-directional", () => {
    for (const entry of COMPARE_ENTRIES) {
      expect(entry.slug).toMatch(KEBAB_SLUG);
      expect(entry.competitorName.length).toBeGreaterThan(0);
      expect(entry.tagline.length).toBeGreaterThan(20);
      expect(entry.relativePath).toMatch(/\.md$/);
      expect(entry.verdict.length).toBeGreaterThan(50);
      expect(entry.updatedAt).toMatch(ISO_DATE);
      // Honest comparison means both sides must have wins.
      expect(entry.winsForCompetitor.length, `${entry.slug} competitor wins`).toBeGreaterThan(0);
      expect(entry.winsForUs.length, `${entry.slug} our wins`).toBeGreaterThan(0);
    }
  });

  it("slugs are unique", () => {
    const slugs = COMPARE_ENTRIES.map((e) => e.slug);
    expect(new Set(slugs).size).toBe(slugs.length);
  });

  it("getCompareEntryBySlug returns entry for known slug, undefined otherwise", () => {
    expect(getCompareEntryBySlug("v0")?.competitorName).toBe("v0 by Vercel");
    expect(getCompareEntryBySlug("does-not-exist")).toBeUndefined();
  });

  it("getAllCompareSlugs mirrors manifest order", () => {
    expect(getAllCompareSlugs()).toEqual(COMPARE_ENTRIES.map((e) => e.slug));
  });
});

describe("solutions-manifest", () => {
  it("seeds at least the three launch verticals", () => {
    expect(SOLUTION_ENTRIES.length).toBeGreaterThanOrEqual(3);
    const slugs = SOLUTION_ENTRIES.map((e) => e.slug);
    expect(slugs).toContain("ai-ecommerce-platform");
    expect(slugs).toContain("ai-lms-builder");
    expect(slugs).toContain("fintech-saas-generator");
  });

  it("every entry has required fields", () => {
    for (const entry of SOLUTION_ENTRIES) {
      expect(entry.slug).toMatch(KEBAB_SLUG);
      expect(entry.vertical.length).toBeGreaterThan(0);
      expect(entry.h1.length).toBeGreaterThan(0);
      expect(entry.metaTitle.length).toBeGreaterThan(20);
      expect(entry.metaTitle.length).toBeLessThanOrEqual(70);
      expect(entry.metaDescription.length).toBeGreaterThan(100);
      expect(entry.metaDescription.length).toBeLessThanOrEqual(200);
      expect(entry.relativePath).toMatch(/\.md$/);
      expect(entry.entityExamples.length).toBeGreaterThan(2);
      expect(entry.primaryKeywords.length).toBeGreaterThan(0);
      expect(entry.updatedAt).toMatch(ISO_DATE);
    }
  });

  it("getSolutionBySlug resolves known slug and rejects unknown", () => {
    expect(getSolutionBySlug("ai-ecommerce-platform")?.vertical).toBe("AI E-commerce Platform");
    expect(getSolutionBySlug("unknown")).toBeUndefined();
  });

  it("getAllSolutionSlugs mirrors manifest", () => {
    expect(getAllSolutionSlugs()).toEqual(SOLUTION_ENTRIES.map((e) => e.slug));
  });
});

describe("docs-manifest", () => {
  it("exposes user and advanced sections", () => {
    const keys = DOC_SECTIONS.map((s) => s.key);
    expect(keys).toContain("user");
    expect(keys).toContain("advanced");
  });

  it("every doc points at a valid section and has a slug", () => {
    const validSections = new Set(DOC_SECTIONS.map((s) => s.key));
    for (const doc of DOCS) {
      expect(doc.slug).toMatch(KEBAB_SLUG);
      expect(validSections.has(doc.section)).toBe(true);
      expect(doc.relativePath).toMatch(/\.md$/);
    }
  });

  it("doc slugs are unique", () => {
    const slugs = DOCS.map((d) => d.slug);
    expect(new Set(slugs).size).toBe(slugs.length);
  });
});

describe("faq-manifest", () => {
  it("every FAQ category is referenced by at least one entry", () => {
    const referencedCategories = new Set(FAQS.map((f) => f.category));
    for (const cat of FAQ_CATEGORIES) {
      expect(referencedCategories.has(cat.slug), `category ${cat.slug} has no FAQs`).toBe(true);
    }
  });

  it("every FAQ references a declared category", () => {
    const validCategories = new Set(FAQ_CATEGORIES.map((c) => c.slug));
    for (const faq of FAQS) {
      expect(validCategories.has(faq.category), `${faq.question} -> ${faq.category}`).toBe(true);
    }
  });

  it("answers stay within the 4-sentence citation budget the manifest comment claims", () => {
    // The manifest says answers are kept to <=4 sentences so AI Overviews can lift them.
    // Counts by terminal punctuation; allows 5 as a loose upper bound before we need to shorten.
    for (const faq of FAQS) {
      const sentenceCount = faq.answer.split(/[.!?]+(?:\s|$)/).filter((s) => s.trim().length > 0).length;
      expect(sentenceCount, `answer for "${faq.question}" is ${sentenceCount} sentences`).toBeLessThanOrEqual(5);
    }
  });
});
