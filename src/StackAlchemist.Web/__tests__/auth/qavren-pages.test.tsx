import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

vi.mock("@/auth", () => ({ signIn: vi.fn() }));
vi.mock("@/lib/supabase", () => ({ supabase: null }));
vi.mock("@/components/oauth-buttons", () => ({ OAuthButtons: () => null }));
vi.mock("next/navigation", () => ({
  redirect: (url: string) => {
    throw new Error(`REDIRECT:${url}`);
  },
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ push: vi.fn() }),
}));

type Params = { error?: string; returnTo?: string };
const searchParams = (p: Params) => ({ searchParams: Promise.resolve(p) });
const qavrenMode = () => vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
const supabaseMode = () => vi.stubEnv("QAVREN_AUTH_URL", "");
const hiddenReturnTo = (container: HTMLElement) =>
  container.querySelector<HTMLInputElement>('input[type="hidden"][name="returnTo"]')?.value;

afterEach(() => {
  vi.unstubAllEnvs();
  vi.resetModules();
});

describe("/login", () => {
  it("Qavren mode: known error copy, sanitised returnTo, hand-off button, register link", async () => {
    qavrenMode();
    const { default: LoginPage } = await import("@/app/login/page");
    const { container } = render(await LoginPage(searchParams({ error: "Configuration", returnTo: "//evil" })));
    expect(screen.getByTestId("login-auth-error")).toHaveTextContent(/temporarily unavailable/i);
    expect(hiddenReturnTo(container)).toBe("/");
    expect(screen.getByRole("button", { name: /sign in/i })).toHaveAttribute("type", "submit");
    expect(screen.getByRole("link", { name: /create an account/i })).toHaveAttribute("href", "/register");
    expect(screen.getByText("localhost:8090")).toBeInTheDocument();
    expect(screen.queryByLabelText(/email/i)).not.toBeInTheDocument();
  });

  it("Qavren mode: a safe returnTo is carried through; an unknown error renders nothing", async () => {
    qavrenMode();
    const { default: LoginPage } = await import("@/app/login/page");
    const { container } = render(await LoginPage(searchParams({ error: "<b>pwned</b>", returnTo: "/dashboard" })));
    expect(hiddenReturnTo(container)).toBe("/dashboard");
    expect(screen.getByRole("link", { name: /create an account/i })).toHaveAttribute(
      "href",
      "/register?returnTo=%2Fdashboard",
    );
    expect(screen.queryByTestId("login-auth-error")).not.toBeInTheDocument();
    expect(screen.queryByText(/pwned/)).not.toBeInTheDocument();
  });

  it("Supabase mode renders the existing client page", async () => {
    supabaseMode();
    const { default: LoginPage } = await import("@/app/login/page");
    render(await LoginPage(searchParams({})));
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
  });
});

describe("/register", () => {
  it("Qavren mode: known error copy, sanitised returnTo, hand-off button, login link", async () => {
    qavrenMode();
    const { default: RegisterPage } = await import("@/app/register/page");
    const { container } = render(await RegisterPage(searchParams({ error: "AccessDenied", returnTo: "https://evil.example" })));
    expect(screen.getByTestId("register-auth-error")).toHaveTextContent(/refused/i);
    expect(hiddenReturnTo(container)).toBe("/");
    expect(screen.getByRole("button", { name: /create your account/i })).toHaveAttribute("type", "submit");
    expect(screen.getByRole("link", { name: /i already have an account/i })).toHaveAttribute("href", "/login");
    expect(screen.queryByLabelText(/email/i)).not.toBeInTheDocument();
  });

  it("Qavren mode: a safe returnTo is carried through to the login link", async () => {
    qavrenMode();
    const { default: RegisterPage } = await import("@/app/register/page");
    const { container } = render(await RegisterPage(searchParams({ error: "nope", returnTo: "/simple" })));
    expect(hiddenReturnTo(container)).toBe("/simple");
    expect(screen.getByRole("link", { name: /i already have an account/i })).toHaveAttribute(
      "href",
      "/login?returnTo=%2Fsimple",
    );
    expect(screen.queryByTestId("register-auth-error")).not.toBeInTheDocument();
  });

  it("Supabase mode renders the existing client page", async () => {
    supabaseMode();
    const { default: RegisterPage } = await import("@/app/register/page");
    render(await RegisterPage(searchParams({})));
    expect(screen.getByLabelText(/confirm password/i)).toBeInTheDocument();
  });
});

describe("Supabase-only password pages", () => {
  it("/forgot-password redirects to /login in Qavren mode", async () => {
    qavrenMode();
    const { default: ForgotPasswordPage } = await import("@/app/forgot-password/page");
    expect(() => ForgotPasswordPage()).toThrow("REDIRECT:/login");
  });

  it("/forgot-password renders the existing client page in Supabase mode", async () => {
    supabaseMode();
    const { default: ForgotPasswordPage } = await import("@/app/forgot-password/page");
    render(ForgotPasswordPage());
    expect(screen.getByRole("button", { name: /send reset link/i })).toBeInTheDocument();
  });

  it("/auth/reset-password redirects to /login in Qavren mode", async () => {
    qavrenMode();
    const { default: ResetPasswordPage } = await import("@/app/auth/reset-password/page");
    expect(() => ResetPasswordPage()).toThrow("REDIRECT:/login");
  });

  it("/auth/reset-password renders the existing client page in Supabase mode", async () => {
    supabaseMode();
    const { default: ResetPasswordPage } = await import("@/app/auth/reset-password/page");
    render(ResetPasswordPage());
    // No Supabase client configured: the client component shows its expired state straight away.
    expect(screen.getByText(/link expired/i)).toBeInTheDocument();
  });
});
