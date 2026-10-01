import { describe, expect, it } from "vitest";
import { AUTH_ERRORS, authErrorMessage } from "@/lib/auth-errors";

describe("authErrorMessage", () => {
  it("maps known Auth.js and app codes to their own copy", () => {
    expect(authErrorMessage("Configuration")).toBe(AUTH_ERRORS.Configuration);
    expect(authErrorMessage("OAuthCallbackError")).toBe(AUTH_ERRORS.OAuthCallbackError);
    expect(authErrorMessage("session_expired")).toBe(AUTH_ERRORS.session_expired);
  });
  it("renders nothing for unknown codes, inherited keys and non-strings", () => {
    for (const bad of ["Your bank needs your password", "", "constructor", "__proto__", "toString"]) {
      expect(authErrorMessage(bad)).toBeNull();
    }
    expect(authErrorMessage(undefined)).toBeNull();
    expect(authErrorMessage(["Default"])).toBeNull();
  });
});
