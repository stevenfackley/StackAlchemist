import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";

vi.mock("@/lib/supabase", () => ({ supabase: null }));
vi.mock("@/lib/runtime-config", () => ({ isDemoMode: true }));
vi.mock("@/components/oauth-buttons", () => ({
  OAuthButtons: () => <div data-testid="oauth-stub" />,
}));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ push: vi.fn() }),
}));

// next/image stub
vi.mock("next/image", () => ({
  // eslint-disable-next-line @next/next/no-img-element
  default: ({ alt }: { alt: string }) => <img alt={alt} />,
}));

// Wrap Suspense for static render
vi.mock("react", async () => {
  const actual = await vi.importActual<typeof import("react")>("react");
  return {
    ...actual,
    Suspense: ({ children }: { children: ReactNode }) => <>{children}</>,
  };
});

import RegisterPage from "@/app/register/RegisterPageClient";

describe("RegisterPageClient a11y", () => {
  it("email input is labelled", () => {
    render(<RegisterPage />);
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
  });

  it("password input is labelled", () => {
    render(<RegisterPage />);
    expect(screen.getByLabelText(/^password$/i)).toBeInTheDocument();
  });

  it("confirm password input is labelled", () => {
    render(<RegisterPage />);
    expect(screen.getByLabelText(/confirm password/i)).toBeInTheDocument();
  });

  it("all three inputs are distinct elements", () => {
    render(<RegisterPage />);
    const email = screen.getByLabelText(/email/i);
    const password = screen.getByLabelText(/^password$/i);
    const confirm = screen.getByLabelText(/confirm password/i);
    expect(email).not.toBe(password);
    expect(password).not.toBe(confirm);
  });
});
