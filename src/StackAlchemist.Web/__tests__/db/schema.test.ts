// @vitest-environment node
import { describe, expect, it } from "vitest";
import { generations } from "@/db/schema";

describe("timestamptz custom type", () => {
  it("normalises Postgres text timestamps to ISO-8601 UTC", () => {
    expect(generations.created_at.mapFromDriverValue("2026-09-29 23:48:25.857111-04")).toBe(
      "2026-09-30T03:48:25.857Z",
    );
  });
});
