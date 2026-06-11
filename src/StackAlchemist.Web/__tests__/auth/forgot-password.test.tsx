import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import ForgotPasswordClient from "@/app/forgot-password/ForgotPasswordClient";

const mockResetPasswordForEmail = vi.fn().mockResolvedValue({ error: null });

vi.mock("@/lib/supabase", () => ({
  supabase: {
    auth: {
      resetPasswordForEmail: (...args: unknown[]) => mockResetPasswordForEmail(...args),
    },
  },
}));

vi.mock("@/lib/runtime-config", () => ({ isDemoMode: false }));

describe("ForgotPasswordClient", () => {
  beforeEach(() => { mockResetPasswordForEmail.mockClear(); });

  it("renders email field and submit button", () => {
    render(<ForgotPasswordClient />);
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /send reset link/i })).toBeInTheDocument();
  });

  it("calls resetPasswordForEmail with the entered email", async () => {
    render(<ForgotPasswordClient />);
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: "test@example.com" } });
    fireEvent.click(screen.getByRole("button", { name: /send reset link/i }));
    await waitFor(() => expect(mockResetPasswordForEmail).toHaveBeenCalledWith(
      "test@example.com",
      expect.objectContaining({ redirectTo: expect.stringContaining("reset-password") })
    ));
  });

  it("always shows success copy after submit (anti-enumeration)", async () => {
    render(<ForgotPasswordClient />);
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: "nobody@example.com" } });
    fireEvent.click(screen.getByRole("button", { name: /send reset link/i }));
    await waitFor(() => expect(screen.getByText(/check your inbox/i)).toBeInTheDocument());
    expect(screen.queryByLabelText(/email/i)).not.toBeInTheDocument();
  });

  it("shows success even when resetPasswordForEmail returns an error", async () => {
    mockResetPasswordForEmail.mockResolvedValueOnce({ error: { message: "User not found" } });
    render(<ForgotPasswordClient />);
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: "ghost@example.com" } });
    fireEvent.click(screen.getByRole("button", { name: /send reset link/i }));
    await waitFor(() => expect(screen.getByText(/check your inbox/i)).toBeInTheDocument());
  });
});
