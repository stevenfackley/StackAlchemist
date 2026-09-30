import { describe, expect, it } from "vitest";
import { isProtectedRoute, safeReturnTo, timingSafeEqual } from "@/lib/proxy-utils";

// ── isProtectedRoute ────────────────────────────────────────────────────────

describe("isProtectedRoute", () => {
  it.each(["/simple", "/advanced", "/generate"])(
    "exact match '%s' is protected",
    (path) => {
      expect(isProtectedRoute(path)).toBe(true);
    },
  );

  it.each([
    "/simple/",
    "/simple/step1",
    "/simple/step1/sub",
    "/advanced/wizard",
    "/generate/abc123",
  ])("prefix match '%s' is protected", (path) => {
    expect(isProtectedRoute(path)).toBe(true);
  });

  it.each(["/", "/login", "/register", "/api/healthz", "/api/csp-report"])(
    "public route '%s' is NOT protected",
    (path) => {
      expect(isProtectedRoute(path)).toBe(false);
    },
  );

  it("'/simplemode' is NOT protected (no prefix false-positive)", () => {
    expect(isProtectedRoute("/simplemode")).toBe(false);
  });

  it("'/advancedstuff' is NOT protected", () => {
    expect(isProtectedRoute("/advancedstuff")).toBe(false);
  });

  it("'/generators' is NOT protected", () => {
    expect(isProtectedRoute("/generators")).toBe(false);
  });

  it("empty string is NOT protected", () => {
    expect(isProtectedRoute("")).toBe(false);
  });
});

// ── timingSafeEqual ─────────────────────────────────────────────────────────

describe("timingSafeEqual", () => {
  it("returns true for identical strings", () => {
    expect(timingSafeEqual("password", "password")).toBe(true);
  });

  it("returns true for empty strings", () => {
    expect(timingSafeEqual("", "")).toBe(true);
  });

  it("returns false for different strings of equal length", () => {
    expect(timingSafeEqual("password", "passworx")).toBe(false);
  });

  it("returns false when lengths differ (short-circuit path)", () => {
    expect(timingSafeEqual("pass", "password")).toBe(false);
  });

  it("returns false for completely different same-length strings", () => {
    expect(timingSafeEqual("aaaa", "bbbb")).toBe(false);
  });

  it("is case-sensitive", () => {
    expect(timingSafeEqual("Secret", "secret")).toBe(false);
  });

  it("handles single character equality", () => {
    expect(timingSafeEqual("x", "x")).toBe(true);
  });

  it("handles single character inequality", () => {
    expect(timingSafeEqual("x", "y")).toBe(false);
  });
});

// ── safeReturnTo ────────────────────────────────────────────────────────────

describe("safeReturnTo", () => {
  it("keeps same-origin absolute paths", () => {
    expect(safeReturnTo("/dashboard")).toBe("/dashboard");
    expect(safeReturnTo("/generate/abc?tier=0")).toBe("/generate/abc?tier=0");
  });
  it("rejects everything that could leave the origin", () => {
    for (const bad of ["//evil.example", "https://evil.example", "/\\evil.example", "dashboard", "", null, undefined, 42]) {
      expect(safeReturnTo(bad)).toBe("/");
    }
    expect(safeReturnTo("//evil", "/dashboard")).toBe("/dashboard");
  });
});
