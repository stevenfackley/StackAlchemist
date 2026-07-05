import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ByokSettingsForm } from "@/app/dashboard/ByokSettingsForm";
import type { ProfileSettings, SaveProfileSettingsState } from "@/lib/types";

const mockSaveProfileSettings = vi.fn();
vi.mock("@/lib/actions", () => ({
  saveProfileSettings: (...args: unknown[]) => mockSaveProfileSettings(...args),
}));

const SETTINGS: ProfileSettings = {
  email: "user@example.com",
  hasApiKeyOverride: false,
  preferredModel: "claude-sonnet-4-6",
};

describe("ByokSettingsForm", () => {
  beforeEach(() => {
    mockSaveProfileSettings.mockReset();
  });

  it("renders the API key override input and preferred model select", () => {
    render(<ByokSettingsForm settings={SETTINGS} />);
    expect(screen.getByLabelText(/api key override/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/preferred model/i)).toBeInTheDocument();
  });

  it("renders an always-present, initially-hidden save-status live region", () => {
    const { container } = render(<ByokSettingsForm settings={SETTINGS} />);
    const liveRegion = container.querySelector('[aria-live="polite"]');
    expect(liveRegion).not.toBeNull();
    expect(liveRegion).toHaveAttribute("hidden");
  });

  it("announces a successful save in the polite live region", async () => {
    mockSaveProfileSettings.mockResolvedValue({
      status: "success",
      message: "Settings saved.",
    } satisfies SaveProfileSettingsState);

    const { container } = render(<ByokSettingsForm settings={SETTINGS} />);
    fireEvent.click(screen.getByRole("button", { name: /save settings/i }));

    await waitFor(() => expect(screen.getByText("Settings saved.")).toBeInTheDocument());

    const liveRegion = container.querySelector('[aria-live="polite"]');
    expect(liveRegion).not.toHaveAttribute("hidden");
    expect(liveRegion).toContainElement(screen.getByText("Settings saved."));
  });

  it("announces a failed save in the same live region", async () => {
    mockSaveProfileSettings.mockResolvedValue({
      status: "error",
      message: "Choose a supported model before saving.",
    } satisfies SaveProfileSettingsState);

    const { container } = render(<ByokSettingsForm settings={SETTINGS} />);
    fireEvent.click(screen.getByRole("button", { name: /save settings/i }));

    await waitFor(() =>
      expect(screen.getByText("Choose a supported model before saving.")).toBeInTheDocument()
    );

    const liveRegion = container.querySelector('[aria-live="polite"]');
    expect(liveRegion).not.toHaveAttribute("hidden");
  });
});
