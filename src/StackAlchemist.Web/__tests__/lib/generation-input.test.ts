/**
 * generation-input.ts — the checks a schema passes before its row is saved,
 * which for a paid tier is before the customer pays.
 */
import { validatePersonalization, validateSchema } from "@/lib/generation-input";
import { DEFAULT_PERSONALIZATION, type Entity, type GenerationSchema } from "@/lib/types";

const id = { name: "id", type: "UUID", pk: true };

function schemaOf(...entities: Entity[]): GenerationSchema {
  return { entities, relationships: [], endpoints: [] };
}

describe("validateSchema", () => {
  it("accepts a schema with distinct, named entities and fields", () => {
    expect(
      validateSchema(
        schemaOf(
          { name: "Invoice", fields: [id, { name: "total", type: "decimal", pk: false }] },
          { name: "Customer", fields: [id] }
        )
      )
    ).toBeNull();
  });

  it("refuses a schema with no entities", () => {
    expect(validateSchema(schemaOf())).toBe("Please define at least one entity before proceeding.");
    expect(validateSchema({} as GenerationSchema)).toBe("Please define at least one entity before proceeding.");
  });

  it("refuses an entity with a blank name", () => {
    expect(validateSchema(schemaOf({ name: "   ", fields: [id] }))).toBe("All entities must have a name.");
  });

  // The Engine strips everything outside [A-Za-z0-9_] and starts at the first
  // letter, so each of these pairs becomes one class, one file and one table.
  it.each([
    ["User", "User"],
    ["User", "user"],
    ["User", "User-"],
    ["Order Item", "OrderItem"],
    ["1Task", "Task"],
    ["!!!", "???"],
  ])("refuses %j and %j, which the Engine would build as one entity", (first, second) => {
    expect(validateSchema(schemaOf({ name: first, fields: [id] }, { name: second, fields: [id] }))).toMatch(
      /^Two entities would both be built as "/
    );
  });

  it("refuses a field with no name, and says which entity it is in", () => {
    expect(
      validateSchema(schemaOf({ name: "Invoice", fields: [id, { name: "", type: "string", pk: false }] }))
    ).toBe("Every field needs a name. Check the fields of Invoice.");
  });

  it("refuses two fields with the same name in one entity, but not across entities", () => {
    const total = { name: "Total", type: "decimal", pk: false };
    expect(validateSchema(schemaOf({ name: "Invoice", fields: [total, { ...total, name: "total" }] }))).toBe(
      'Invoice has two fields named "total". Give each field its own name.'
    );
    expect(
      validateSchema(schemaOf({ name: "Invoice", fields: [total] }, { name: "Quote", fields: [total] }))
    ).toBeNull();
  });

  it("refuses names longer than 64 characters", () => {
    const long = "A".repeat(65);
    expect(validateSchema(schemaOf({ name: long, fields: [id] }))).toBe("Entity names can be at most 64 characters.");
    expect(validateSchema(schemaOf({ name: "Invoice", fields: [{ ...id, name: long }] }))).toBe(
      "Field names can be at most 64 characters."
    );
  });

  it("refuses a schema over 100 KB", () => {
    const entities = Array.from({ length: 400 }, (_, e) => ({
      name: `Entity${e}`,
      fields: Array.from({ length: 10 }, (_, f) => ({ name: `field_${f}`, type: "string", pk: false })),
    }));

    expect(validateSchema(schemaOf(...entities))).toBe(
      "This schema is too large to build. Remove some entities or fields and try again."
    );
  });
});

describe("validatePersonalization", () => {
  it("accepts none, and the defaults", () => {
    expect(validatePersonalization(undefined)).toBeNull();
    expect(validatePersonalization(DEFAULT_PERSONALIZATION)).toBeNull();
  });

  it("refuses personalization over 16 KB", () => {
    expect(
      validatePersonalization({ ...DEFAULT_PERSONALIZATION, businessDescription: "x".repeat(17 * 1024) })
    ).toBe("The personalization details are too long. Shorten them and try again.");
  });
});
