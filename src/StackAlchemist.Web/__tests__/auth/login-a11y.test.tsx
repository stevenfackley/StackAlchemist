import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";

vi.mock("@/lib/supabase", () => ({ supabase: null }));
vi.mock("@/lib/runtime-config", () => ({ isDemoMode: true }));
vi.mock("@/components/oauth-buttons", () => ({ OAuthButtons: () => null }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ push: vi.fn() }),
}));
vi.mock("next/image", () => ({
  // eslint-disable-next-line @next/next/no-img-element
  default: ({ alt }: { alt: string }) => <img alt={alt} />,
}));
vi.mock("react", async () => {
  const actual = await vi.importActual<typeof import("react")>("react");
  return {
    ...actual,
    Suspense: ({ children }: { children: ReactNode }) => <>{children}</>,
  };
});

import LoginPage from "@/app/login/LoginPageClient";

describe("LoginPageClient a11y", () => {
  it("email input is labelled", () => {
    render(<LoginPage />);
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
  });

  it("password input is labelled", () => {
    render(<LoginPage />);
    expect(screen.getByLabelText(/^password$/i)).toBeInTheDocument();
  });

  it("email and password inputs are distinct elements", () => {
    render(<LoginPage />);
    const email = screen.getByLabelText(/email/i);
    const password = screen.getByLabelText(/^password$/i);
    expect(email).not.toBe(password);
  });
});
