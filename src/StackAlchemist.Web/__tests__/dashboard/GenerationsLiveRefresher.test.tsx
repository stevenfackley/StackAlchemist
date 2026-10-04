import { render, act } from "@testing-library/react";
import { vi, beforeEach, afterEach } from "vitest";

const { mockRefresh } = vi.hoisted(() => ({ mockRefresh: vi.fn() }));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: mockRefresh }),
}));

import { GenerationsLiveRefresher, DASHBOARD_REFRESH_MS } from "@/app/dashboard/GenerationsLiveRefresher";

function setVisibility(state: DocumentVisibilityState) {
  Object.defineProperty(document, "visibilityState", { value: state, configurable: true });
  document.dispatchEvent(new Event("visibilitychange"));
}

describe("GenerationsLiveRefresher", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mockRefresh.mockReset();
    Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders null (no DOM output)", () => {
    const { container } = render(<GenerationsLiveRefresher active />);
    expect(container.firstChild).toBeNull();
  });

  it("refreshes every 10s while a build is in progress", () => {
    expect(DASHBOARD_REFRESH_MS).toBe(10_000);
    render(<GenerationsLiveRefresher active />);
    expect(mockRefresh).not.toHaveBeenCalled();
    act(() => vi.advanceTimersByTime(DASHBOARD_REFRESH_MS));
    expect(mockRefresh).toHaveBeenCalledTimes(1);
    act(() => vi.advanceTimersByTime(DASHBOARD_REFRESH_MS));
    expect(mockRefresh).toHaveBeenCalledTimes(2);
  });

  it("does not poll when nothing is in progress", () => {
    render(<GenerationsLiveRefresher active={false} />);
    act(() => vi.advanceTimersByTime(DASHBOARD_REFRESH_MS * 5));
    expect(mockRefresh).not.toHaveBeenCalled();
  });

  it("stops polling once the in-progress builds finish", () => {
    const { rerender } = render(<GenerationsLiveRefresher active />);
    rerender(<GenerationsLiveRefresher active={false} />);
    act(() => vi.advanceTimersByTime(DASHBOARD_REFRESH_MS * 3));
    expect(mockRefresh).not.toHaveBeenCalled();
  });

  it("pauses while hidden, refreshes on return and resumes", () => {
    render(<GenerationsLiveRefresher active />);
    act(() => setVisibility("hidden"));
    act(() => vi.advanceTimersByTime(DASHBOARD_REFRESH_MS * 3));
    expect(mockRefresh).not.toHaveBeenCalled();

    act(() => setVisibility("visible"));
    expect(mockRefresh).toHaveBeenCalledTimes(1);
    act(() => vi.advanceTimersByTime(DASHBOARD_REFRESH_MS));
    expect(mockRefresh).toHaveBeenCalledTimes(2);
  });

  it("refreshes on return to the tab even with nothing in progress (a build may have started elsewhere)", () => {
    render(<GenerationsLiveRefresher active={false} />);
    act(() => setVisibility("hidden"));
    act(() => setVisibility("visible"));
    expect(mockRefresh).toHaveBeenCalledTimes(1);
  });

  it("clears its timer and listener on unmount", () => {
    const { unmount } = render(<GenerationsLiveRefresher active />);
    unmount();
    act(() => vi.advanceTimersByTime(DASHBOARD_REFRESH_MS * 3));
    act(() => setVisibility("visible"));
    expect(mockRefresh).not.toHaveBeenCalled();
  });
});
