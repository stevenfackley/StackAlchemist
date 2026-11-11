import type { GenerationSchema, PersonalizationData } from "./types";

/**
 * Checks on what a customer asks to have built, run before the row is saved.
 * For a paid tier that is before the payment: the Stripe webhook builds whatever
 * the row holds, so a schema that cannot be built must be refused here, not
 * discovered after the charge.
 */

const MAX_NAME_LENGTH = 64;
const MAX_SCHEMA_BYTES = 100 * 1024;
const MAX_PERSONALIZATION_BYTES = 16 * 1024;

/**
 * The identifier the Engine makes of an entity or field name
 * (TemplateValueSanitizer.SanitizeIdentifier, called from BuildVariables with
 * the fallbacks "Entity" and "Field"): only A-Z, a-z, 0-9 and "_" survive, and
 * it starts at the first letter. Two names that differ only in what is stripped
 * ("User" and "User-", "email" and "e-mail") become one class or one property,
 * and the build fails on the duplicate.
 */
function engineIdentifier(name: string, fallback: "Entity" | "Field"): string {
  return name.replace(/[^A-Za-z0-9_]/g, "").replace(/^[^A-Za-z]+/, "") || fallback;
}

function byteLength(value: unknown): number {
  return new TextEncoder().encode(JSON.stringify(value)).length;
}

/** The reason a schema cannot be built, in words for the customer; null when it can. */
export function validateSchema(schema: GenerationSchema): string | null {
  if (!Array.isArray(schema?.entities) || schema.entities.length === 0) {
    return "Please define at least one entity before proceeding.";
  }

  const seen = new Set<string>();
  for (const entity of schema.entities) {
    const name = typeof entity?.name === "string" ? entity.name.trim() : "";
    if (!name) {
      return "All entities must have a name.";
    }
    if (name.length > MAX_NAME_LENGTH) {
      return `Entity names can be at most ${MAX_NAME_LENGTH} characters.`;
    }

    const identifier = engineIdentifier(name, "Entity");
    if (seen.has(identifier.toLowerCase())) {
      return `Two entities would both be built as "${identifier}". Give each entity its own name.`;
    }
    seen.add(identifier.toLowerCase());

    const fieldNames = new Set<string>();
    for (const field of entity.fields ?? []) {
      const fieldName = typeof field?.name === "string" ? field.name.trim() : "";
      if (!fieldName) {
        return `Every field needs a name. Check the fields of ${name}.`;
      }
      if (fieldName.length > MAX_NAME_LENGTH) {
        return `Field names can be at most ${MAX_NAME_LENGTH} characters.`;
      }
      const fieldIdentifier = engineIdentifier(fieldName, "Field");
      if (fieldNames.has(fieldIdentifier.toLowerCase())) {
        return `${name} has two fields that would both be built as "${fieldIdentifier}". Give each field its own name.`;
      }
      fieldNames.add(fieldIdentifier.toLowerCase());
    }
  }

  if (byteLength(schema) > MAX_SCHEMA_BYTES) {
    return "This schema is too large to build. Remove some entities or fields and try again.";
  }

  return null;
}

/** The reason the personalization cannot be saved; null when it can. */
export function validatePersonalization(personalization: PersonalizationData | undefined): string | null {
  if (personalization && byteLength(personalization) > MAX_PERSONALIZATION_BYTES) {
    return "The personalization details are too long. Shorten them and try again.";
  }
  return null;
}
