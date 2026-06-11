import { render } from "@testing-library/react";
import { vi, beforeEach, afterEach } from "vitest";

// ── Hoisted mocks (must be declared before vi.mock factories run) ─────────────

const { mockRefresh, mockChannel, mockRemoveChannel } = vi.hoisted(() => {
  const mockRefresh = vi.fn();
  const mockRemoveChannel = vi.fn();
  const mockChannel = {
    on: vi.fn().mockReturnThis(),
    subscribe: vi.fn().mockReturnThis(),
  };
  return { mockRefresh, mockChannel, mockRemoveChannel };
});

vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: mockRefresh }),
}));

vi.mock("@/lib/supabase", () => ({
  supabase: {
    channel: vi.fn(() => mockChannel),
    removeChannel: mockRemoveChannel,
  },
}));

vi.mock("@/lib/runtime-config", () => ({ isDemoMode: false }));

// ── Import component AFTER mocks are set up ───────────────────────────────────

import { GenerationsLiveRefresher } from "@/app/dashboard/GenerationsLiveRefresher";

// ── Tests ─────────────────────────────────────────────────────────────────────

type ChangeCallback = (payload: { new: Record<string, unknown> }) => void;
let capturedChangeCallback: ChangeCallback | null = null;
let capturedVisibilityCallback: (() => void) | null = null;

describe("GenerationsLiveRefresher", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    capturedChangeCallback = null;
    capturedVisibilityCallback = null;

    mockChannel.on.mockImplementation(
      (_event: string, _filter: unknown, cb: ChangeCallback) => {
        capturedChangeCallback = cb;
        return mockChannel;
      }
    );

    vi.spyOn(document, "addEventListener").mockImplementation(
      (event: string, cb: EventListenerOrEventListenerObject) => {
        if (event === "visibilitychange") {
          capturedVisibilityCallback = cb as () => void;
        }
      }
    );
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it("renders null (no DOM output)", () => {
    const { container } = render(<GenerationsLiveRefresher userId="user-1" />);
    expect(container.firstChild).toBeNull();
  });

  it("subscribes to the correct channel and table filter", async () => {
    const { supabase } = await vi.importMock<typeof import("@/lib/supabase")>("@/lib/supabase");
    render(<GenerationsLiveRefresher userId="user-42" />);
    expect((supabase as { channel: ReturnType<typeof vi.fn> }).channel).toHaveBeenCalledWith("gen-feed:user-42");
    expect(mockChannel.on).toHaveBeenCalledWith(
      "postgres_changes",
      expect.objectContaining({
        table: "generations",
        filter: "user_id=eq.user-42",
      }),
      expect.any(Function)
    );
  });

  it("calls router.refresh() when a matching change arrives", () => {
    vi.useFakeTimers();
    render(<GenerationsLiveRefresher userId="user-1" />);
    capturedChangeCallback?.({ new: { user_id: "user-1" } });
    expect(mockRefresh).toHaveBeenCalledOnce();
  });

  it("throttles: second call within 5s is dropped", () => {
    vi.useFakeTimers();
    render(<GenerationsLiveRefresher userId="user-1" />);
    capturedChangeCallback?.({ new: { user_id: "user-1" } });
    capturedChangeCallback?.({ new: { user_id: "user-1" } });
    expect(mockRefresh).toHaveBeenCalledOnce();
  });

  it("throttle resets after 5s", () => {
    vi.useFakeTimers();
    render(<GenerationsLiveRefresher userId="user-1" />);
    capturedChangeCallback?.({ new: { user_id: "user-1" } });
    vi.advanceTimersByTime(5001);
    capturedChangeCallback?.({ new: { user_id: "user-1" } });
    expect(mockRefresh).toHaveBeenCalledTimes(2);
  });

  it("ignores events for a different user_id", () => {
    vi.useFakeTimers();
    render(<GenerationsLiveRefresher userId="user-1" />);
    capturedChangeCallback?.({ new: { user_id: "user-999" } });
    expect(mockRefresh).not.toHaveBeenCalled();
  });

  it("refreshes on visibilitychange to visible", () => {
    vi.useFakeTimers();
    render(<GenerationsLiveRefresher userId="user-1" />);
    Object.defineProperty(document, "visibilityState", {
      value: "visible",
      configurable: true,
    });
    capturedVisibilityCallback?.();
    expect(mockRefresh).toHaveBeenCalledOnce();
  });
});
