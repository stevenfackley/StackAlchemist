import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import PrivacyPage from "@/app/privacy/page";

describe("privacy page auth-processor copy", () => {
  it("names Qavren Auth (Keycloak) and keeps Supabase as the database", () => {
    render(PrivacyPage());
    expect(screen.getByText(/Auth is handled by/)).toHaveTextContent(/Qavren Auth/);
    expect(screen.getAllByText(/Qavren Auth/).length).toBeGreaterThan(0);
    // The strong tag is the match; the row it sits in carries the role.
    const supabase = screen.getByText("Supabase", { selector: "li > strong" }).closest("li");
    expect(supabase).toHaveTextContent(/database/);
    expect(supabase).not.toHaveTextContent(/authentication/);
    expect(screen.queryByText(/refresh tokens from Supabase/)).toBeNull();
  });

  it("describes the session cookie without depending on QAVREN_AUTH_URL (the page is static)", () => {
    render(PrivacyPage());
    expect(screen.getByText(/encrypted session\s+cookie/)).toBeInTheDocument();
  });
});
