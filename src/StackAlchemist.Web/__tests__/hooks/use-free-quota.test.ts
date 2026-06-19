import { renderHook, waitFor } from "@testing-library/react";
import { useFreeQuota } from "@/lib/hooks/use-free-quota";
import type { FreeQuotaStatus } from "@/lib/actions";

const getFreeQuotaStatusMock = vi.fn<() => Promise<FreeQuotaStatus>>();

vi.mock("@/lib/actions", () => ({
  getFreeQuotaStatus: (...args: unknown[]) => getFreeQuotaStatusMock(...args),
}));

const SAMPLE_QUOTA: FreeQuotaStatus = {
  limit: 5,
  used: 2,
  remaining: 3,
  resetsAt: "2026-07-01T00:00:00.000Z",
  resetsAtLabel: "July 1",
};

describe("useFreeQuota", () => {
  beforeEach(() => {
    getFreeQuotaStatusMock.mockReset();
  });

  it("starts with { quota: null, loading: true }", () => {
    // Keep the promise pending for the duration of this assertion.
    getFreeQuotaStatusMock.mockReturnValue(new Promise(() => {}));

    const { result } = renderHook(() => useFreeQuota());

    expect(result.current.quota).toBeNull();
    expect(result.current.loading).toBe(true);
  });

  it("after successful resolution returns { quota: <value>, loading: false }", async () => {
    getFreeQuotaStatusMock.mockResolvedValue(SAMPLE_QUOTA);

    const { result } = renderHook(() => useFreeQuota());

    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.quota).toEqual(SAMPLE_QUOTA);
    expect(result.current.loading).toBe(false);
  });

  it("on rejection returns { quota: null, loading: false } (error swallowed)", async () => {
    getFreeQuotaStatusMock.mockRejectedValue(new Error("network error"));

    const { result } = renderHook(() => useFreeQuota());

    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.quota).toBeNull();
    expect(result.current.loading).toBe(false);
  });
});
