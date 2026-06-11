import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import ResetPasswordPage from "@/app/auth/reset-password/page";

const mockGetSession = vi.fn();
const mockUpdateUser = vi.fn();
let capturedAuthCallback: ((event: string, session: unknown) => void) | null = null;

vi.mock("@/lib/supabase", () => ({
  supabase: {
    auth: {
      getSession: (...args: unknown[]) => mockGetSession(...args),
      onAuthStateChange: (cb: (event: string, session: unknown) => void) => {
        capturedAuthCallback = cb;
        return { data: { subscription: { unsubscribe: vi.fn() } } };
      },
      updateUser: (...args: unknown[]) => mockUpdateUser(...args),
    },
  },
}));

vi.mock("@/lib/runtime-config", () => ({ isDemoMode: false }));

describe("ResetPasswordPage", () => {
  beforeEach(() => {
    capturedAuthCallback = null;
    mockGetSession.mockResolvedValue({ data: { session: null } });
    mockUpdateUser.mockResolvedValue({ error: null });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows form when getSession resolves with a session", async () => {
    mockGetSession.mockResolvedValue({ data: { session: { user: { id: "u1" } } } });
    render(<ResetPasswordPage />);
    // getByRole("heading") is unambiguous — button with same text doesn't match "heading" role
    await waitFor(
      () => expect(screen.getByRole("heading", { name: /set new password/i })).toBeInTheDocument(),
      { timeout: 3000 }
    );
  });

  it("shows form when PASSWORD_RECOVERY event fires", async () => {
    render(<ResetPasswordPage />);
    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
      capturedAuthCallback?.("PASSWORD_RECOVERY", { user: { id: "u1" } });
    });
    await waitFor(
      () => expect(screen.getByRole("heading", { name: /set new password/i })).toBeInTheDocument(),
      { timeout: 3000 }
    );
  });

  it("shows expired panel after 4s timeout with no session", async () => {
    vi.useFakeTimers();
    render(<ResetPasswordPage />);
    // Flush the null-session getSession promise
    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });
    // Advance past the 4s timeout; act() flushes the resulting state update
    await act(async () => {
      vi.advanceTimersByTime(5000);
    });
    // Don't use waitFor here — its internal setTimeout is also faked
    expect(screen.getByText(/link expired/i)).toBeInTheDocument();
  });

  it("validates password mismatch before calling updateUser", async () => {
    mockGetSession.mockResolvedValue({ data: { session: { user: { id: "u1" } } } });
    render(<ResetPasswordPage />);
    await waitFor(() => screen.getByLabelText(/new password/i), { timeout: 3000 });
    fireEvent.change(screen.getByLabelText(/new password/i), { target: { value: "longpassword1" } });
    fireEvent.change(screen.getByLabelText(/confirm password/i), { target: { value: "different123456" } });
    fireEvent.click(screen.getByRole("button", { name: /set new password/i }));
    expect(screen.getByText(/passwords do not match/i)).toBeInTheDocument();
    expect(mockUpdateUser).not.toHaveBeenCalled();
  });

  it("calls updateUser with the new password on valid submit", async () => {
    mockGetSession.mockResolvedValue({ data: { session: { user: { id: "u1" } } } });
    render(<ResetPasswordPage />);
    await waitFor(() => screen.getByLabelText(/new password/i), { timeout: 3000 });
    fireEvent.change(screen.getByLabelText(/new password/i), { target: { value: "newstrongpass" } });
    fireEvent.change(screen.getByLabelText(/confirm password/i), { target: { value: "newstrongpass" } });
    fireEvent.click(screen.getByRole("button", { name: /set new password/i }));
    await waitFor(() => expect(mockUpdateUser).toHaveBeenCalledWith({ password: "newstrongpass" }),
      { timeout: 3000 });
  });
});
