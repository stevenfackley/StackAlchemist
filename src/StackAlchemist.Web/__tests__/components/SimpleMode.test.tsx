import { render, screen, waitFor } from "@testing-library/react";
import type { Generation } from "@/lib/types";

const submitMock = vi.fn();
vi.mock("@/lib/actions", () => ({
  submitSimpleGeneration: (...args: unknown[]) => submitMock(...args),
}));

// Capture the realtime watcher's onUpdate so tests can drive row updates.
const realtimeCalls: Array<{ generationId: string | null; onUpdate: (row: Generation) => void }> = [];
vi.mock("@/lib/hooks/use-generation-realtime", () => ({
  useGenerationRealtime: (opts: {
    generationId: string | null;
    onUpdate: (row: Generation) => void;
  }) => {
    realtimeCalls.push(opts);
    return { transport: "realtime" as const };
  },
}));

vi.mock("@/lib/supabase", () => ({ supabase: {} }));
vi.mock("@/lib/runtime-config", () => ({ isDemoMode: false }));

const searchParamsMock = vi.hoisted(() => ({ value: new URLSearchParams() }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => searchParamsMock.value,
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), refresh: vi.fn() }),
  usePathname: () => "/simple",
}));

import SimpleModePage from "@/app/simple/SimpleModePage";

function makeRow(status: Generation["status"], error?: string): Generation {
  return { id: "gen-1", status, error_message: error ?? null } as Generation;
}

describe("SimpleModePage", () => {
  const originalLocation = window.location;

  beforeEach(() => {
    submitMock.mockReset();
    realtimeCalls.length = 0;
    searchParamsMock.value = new URLSearchParams();
    Object.defineProperty(window, "location", {
      writable: true,
      value: { ...originalLocation, href: "http://localhost/simple" },
    });
  });

  afterEach(() => {
    Object.defineProperty(window, "location", { writable: true, value: originalLocation });
  });

  it("shows the error phase and does not submit when q is missing", () => {
    render(<SimpleModePage />);
    expect(screen.getByTestId("simple-phase-error")).toBeInTheDocument();
    expect(submitMock).not.toHaveBeenCalled();
  });

  it("submits the prompt and redirects when the watcher reports success", async () => {
    searchParamsMock.value = new URLSearchParams("q=Build+me+an+app");
    submitMock.mockResolvedValue({
      success: true,
      generationId: "gen-1",
      redirectUrl: "/generate/gen-1",
    });

    render(<SimpleModePage />);

    await waitFor(() => expect(submitMock).toHaveBeenCalledWith("Build me an app", 0));
    await waitFor(() =>
      expect(realtimeCalls.some((c) => c.generationId === "gen-1")).toBe(true),
    );

    const watcher = realtimeCalls.find((c) => c.generationId === "gen-1")!;
    watcher.onUpdate(makeRow("success"));

    expect(window.location.href).toBe("/generate/gen-1");
  });

  it("shows the error phase when the watcher reports failure", async () => {
    searchParamsMock.value = new URLSearchParams("q=Build+me+an+app");
    submitMock.mockResolvedValue({
      success: true,
      generationId: "gen-1",
      redirectUrl: "/generate/gen-1",
    });

    render(<SimpleModePage />);
    await waitFor(() =>
      expect(realtimeCalls.some((c) => c.generationId === "gen-1")).toBe(true),
    );

    const watcher = realtimeCalls.find((c) => c.generationId === "gen-1")!;
    watcher.onUpdate(makeRow("failed", "Schema validation failed"));

    await waitFor(() => expect(screen.getByTestId("simple-phase-error")).toBeInTheDocument());
    expect(screen.getByText("Schema validation failed")).toBeInTheDocument();
  });

  it("acts only once when success arrives twice (live event + catch-up race)", async () => {
    searchParamsMock.value = new URLSearchParams("q=Build+me+an+app");
    submitMock.mockResolvedValue({
      success: true,
      generationId: "gen-1",
      redirectUrl: "/generate/gen-1",
    });

    render(<SimpleModePage />);
    await waitFor(() =>
      expect(realtimeCalls.some((c) => c.generationId === "gen-1")).toBe(true),
    );

    const watcher = realtimeCalls.find((c) => c.generationId === "gen-1")!;
    watcher.onUpdate(makeRow("success"));
    window.location.href = "http://localhost/somewhere-else";
    watcher.onUpdate(makeRow("success"));

    // The done-guard swallowed the second event: no second redirect.
    expect(window.location.href).toBe("http://localhost/somewhere-else");
  });
});
