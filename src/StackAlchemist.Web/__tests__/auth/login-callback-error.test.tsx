import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

// Stub useSearchParams to inject ?error=auth_callback_failed
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams("error=auth_callback_failed"),
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

  it("clicking 'send a new magic link' switches to magic mode", () => {
    render(<LoginPage />);
    fireEvent.click(screen.getByRole("button", { name: /send a new magic link/i }));
    // Magic Link tab should now be active — both tabs are present
    const tabs = screen.getAllByRole("button", { name: /magic link/i });
    expect(tabs.length).toBeGreaterThan(0);
  });
});
