// @vitest-environment node
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { generations } from "@/db/schema";

describe("timestamptz custom type", () => {
  it("normalises Postgres text timestamps to ISO-8601 UTC", () => {
    expect(generations.created_at.mapFromDriverValue("2026-09-29 23:48:25.857111-04")).toBe(
      "2026-09-30T03:48:25.857Z",
    );
  });
});

describe("generations_status_check", () => {
  // The Engine writes these names and sweeps the non-terminal ones (GenerationStatus.cs). A
  // status the Engine writes and the CHECK refuses is a failed write; one the CHECK allows and
  // the Engine does not list is a row its reconciler never sees.
  it("allows exactly the statuses the Engine's GenerationStatus.All lists, in the same order", () => {
    const schema = readFileSync(resolve(__dirname, "../../src/db/schema.ts"), "utf8");
    const check = /check\("generations_status_check", sql`\$\{t\.status\} in \(([^)]*)\)`\)/.exec(schema);
    expect(check).not.toBeNull();
    const allowed = [...check![1].matchAll(/'([a-z_]+)'/g)].map(([, status]) => status);

    const engine = readFileSync(
      resolve(__dirname, "../../../StackAlchemist.Engine/Models/GenerationStatus.cs"), "utf8");
    const all = /IReadOnlyList<string> All =\s*\[([^\]]*)\]/.exec(engine);
    expect(all).not.toBeNull();
    const engineAll = all![1].split(",").map((name) => name.trim()).filter(Boolean)
      .map((name) => new RegExp(`const string ${name} = "([a-z_]+)"`).exec(engine)?.[1]);

    expect(allowed.length).toBeGreaterThan(0);
    expect(engineAll).toEqual(allowed);
  });
});
