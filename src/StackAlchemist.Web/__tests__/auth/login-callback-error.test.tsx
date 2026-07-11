import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

// Stub useSearchParams to inject ?error=auth_callback_failed (mutable so
// individual tests can add ?reason=)
const mockParams = vi.hoisted(() => ({ value: "error=auth_callback_failed" }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(mockParams.value),
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("@/lib/supabase", () => ({ supabase: null }));
vi.mock("@/lib/runtime-config", () => ({ isDemoMode: true }));
vi.mock("@/components/oauth-buttons", () => ({ OAuthButtons: () => null }));

// Import AFTER mocks are in place
import LoginPage from "@/app/login/LoginPageClient";

describe("Login — ?error=auth_callback_failed", () => {
  it("renders the callback-error alert", () => {
    render(<LoginPage />);
    expect(screen.getByTestId("login-callback-error")).toBeInTheDocument();
    expect(screen.getByText(/sign-in link expired or invalid/i)).toBeInTheDocument();
  });

  it("shows the failure reason when /auth/callback forwards one", () => {
    mockParams.value = "error=auth_callback_failed&reason=flow_state_not_found";
    render(<LoginPage />);
    expect(screen.getByText(/flow_state_not_found/)).toBeInTheDocument();
    mockParams.value = "error=auth_callback_failed";
  });

  it("clicking 'send a new magic link' switches to magic mode", () => {
    render(<LoginPage />);
    fireEvent.click(screen.getByRole("button", { name: /send a new magic link/i }));
    // Magic Link tab should now be active — both tabs are present
    const tabs = screen.getAllByRole("button", { name: /magic link/i });
    expect(tabs.length).toBeGreaterThan(0);
  });
});
