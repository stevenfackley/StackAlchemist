import { renderHook, act } from "@testing-library/react";
import { useGenerationStatus, GENERATION_POLL_MS } from "@/lib/hooks/use-generation-status";

const getGenerationMock = vi.fn();
vi.mock("@/lib/actions", () => ({
  getGeneration: (...args: unknown[]) => getGenerationMock(...args),
}));

function setVisibility(state: DocumentVisibilityState) {
  Object.defineProperty(document, "visibilityState", { value: state, configurable: true });
  document.dispatchEvent(new Event("visibilitychange"));
}

async function advance(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

describe("useGenerationStatus", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getGenerationMock.mockReset();
    getGenerationMock.mockResolvedValue(null);
    Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("does nothing without a generation id", async () => {
    renderHook(() => useGenerationStatus({ generationId: null, onUpdate: vi.fn() }));
    await advance(GENERATION_POLL_MS * 3);
    expect(getGenerationMock).not.toHaveBeenCalled();
  });

  it("does nothing when disabled", async () => {
    renderHook(() => useGenerationStatus({ generationId: "g0", enabled: false, onUpdate: vi.fn() }));
    await advance(GENERATION_POLL_MS * 3);
    expect(getGenerationMock).not.toHaveBeenCalled();
  });

  it("polls every 3s by default", () => {
    expect(GENERATION_POLL_MS).toBe(3_000);
  });

  it("fetches immediately, then on every tick, and hands rows to onUpdate", async () => {
    const row = { id: "g1", status: "building" };
    getGenerationMock.mockResolvedValue(row);
    const onUpdate = vi.fn();

    renderHook(() => useGenerationStatus({ generationId: "g1", onUpdate, pollMs: 1000 }));
    await advance(0);
    expect(getGenerationMock).toHaveBeenCalledTimes(1);
    expect(getGenerationMock).toHaveBeenCalledWith("g1");
    expect(onUpdate).toHaveBeenCalledWith(row);

    await advance(1000);
    expect(getGenerationMock).toHaveBeenCalledTimes(2);
    await advance(1000);
    expect(getGenerationMock).toHaveBeenCalledTimes(3);
  });

  it("does not call onUpdate for a null row (not found / not the owner)", async () => {
    const onUpdate = vi.fn();
    renderHook(() => useGenerationStatus({ generationId: "g2", onUpdate, pollMs: 1000 }));
    await advance(1000);
    expect(getGenerationMock).toHaveBeenCalled();
    expect(onUpdate).not.toHaveBeenCalled();
  });

  it("pauses while the tab is hidden and catches up when it is visible again", async () => {
    renderHook(() => useGenerationStatus({ generationId: "g3", onUpdate: vi.fn(), pollMs: 1000 }));
    await advance(0);
    expect(getGenerationMock).toHaveBeenCalledTimes(1);

    act(() => setVisibility("hidden"));
    await advance(5000);
    expect(getGenerationMock).toHaveBeenCalledTimes(1);

    act(() => setVisibility("visible"));
    await advance(0);
    expect(getGenerationMock).toHaveBeenCalledTimes(2);
    await advance(1000);
    expect(getGenerationMock).toHaveBeenCalledTimes(3);
  });

  it("starts paused when mounted in a hidden tab, after one initial fetch", async () => {
    Object.defineProperty(document, "visibilityState", { value: "hidden", configurable: true });
    renderHook(() => useGenerationStatus({ generationId: "g4", onUpdate: vi.fn(), pollMs: 1000 }));
    await advance(5000);
    expect(getGenerationMock).toHaveBeenCalledTimes(1);
  });

  it("swallows a failed fetch and recovers on the next tick", async () => {
    const row = { id: "g5", status: "packing" };
    getGenerationMock.mockRejectedValueOnce(new Error("network")).mockResolvedValue(row);
    const onUpdate = vi.fn();

    renderHook(() => useGenerationStatus({ generationId: "g5", onUpdate, pollMs: 1000 }));
    await advance(0);
    expect(onUpdate).not.toHaveBeenCalled();
    await advance(1000);
    expect(onUpdate).toHaveBeenCalledWith(row);
  });

  it("never stacks requests while one is still in flight", async () => {
    getGenerationMock.mockReturnValue(new Promise(() => {}));
    renderHook(() => useGenerationStatus({ generationId: "g6", onUpdate: vi.fn(), pollMs: 1000 }));
    await advance(5000);
    expect(getGenerationMock).toHaveBeenCalledTimes(1);
  });

  it("stops when disabled (terminal status) and when unmounted", async () => {
    const { rerender, unmount } = renderHook(
      ({ enabled }) => useGenerationStatus({ generationId: "g7", enabled, onUpdate: vi.fn(), pollMs: 1000 }),
      { initialProps: { enabled: true } },
    );
    await advance(1000);
    const callsWhileEnabled = getGenerationMock.mock.calls.length;

    rerender({ enabled: false });
    await advance(5000);
    expect(getGenerationMock).toHaveBeenCalledTimes(callsWhileEnabled);

    rerender({ enabled: true });
    await advance(0);
    unmount();
    const callsAtUnmount = getGenerationMock.mock.calls.length;
    await advance(5000);
    expect(getGenerationMock).toHaveBeenCalledTimes(callsAtUnmount);
  });

  it("drops a row that resolves after unmount", async () => {
    let resolve: (row: unknown) => void = () => {};
    getGenerationMock.mockReturnValue(new Promise((r) => { resolve = r; }));
    const onUpdate = vi.fn();
    const { unmount } = renderHook(() => useGenerationStatus({ generationId: "g8", onUpdate }));
    unmount();
    await act(async () => resolve({ id: "g8", status: "success" }));
    expect(onUpdate).not.toHaveBeenCalled();
  });
});
