import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

describe("privacy page auth-processor copy", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it("names Supabase as the auth processor in Supabase mode", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "");
    const { default: PrivacyPage } = await import("@/app/privacy/page");
    render(PrivacyPage());
    expect(screen.getByText(/Auth is handled by/)).toHaveTextContent(/Supabase/);
    expect(screen.queryByText(/Qavren Auth/)).toBeNull();
    expect(screen.getByText(/authentication and database/)).toHaveTextContent(/Supabase/);
    expect(screen.getByText(/refresh tokens from Supabase/)).toBeInTheDocument();
  });

  it("names Qavren Auth (Keycloak) in Qavren mode and keeps Supabase as the database", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    const { default: PrivacyPage } = await import("@/app/privacy/page");
    render(PrivacyPage());
    expect(screen.getByText(/Auth is handled by/)).toHaveTextContent(/Qavren Auth/);
    expect(screen.getAllByText(/Qavren Auth/).length).toBeGreaterThan(0);
    // The strong tag is the match; the row it sits in carries the role.
    const supabase = screen.getByText("Supabase", { selector: "li > strong" }).closest("li");
    expect(supabase).toHaveTextContent(/database/);
    expect(supabase).not.toHaveTextContent(/authentication/);
    expect(screen.queryByText(/refresh tokens from Supabase/)).toBeNull();
  });
});
