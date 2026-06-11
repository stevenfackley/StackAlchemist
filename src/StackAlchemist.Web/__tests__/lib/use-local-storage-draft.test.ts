import { renderHook, act } from "@testing-library/react";
import { useLocalStorageDraft } from "@/lib/hooks/use-local-storage-draft";

const KEY = "test:draft:v1";

function seed(data: unknown, { v = 1, ageMs = 0 }: { v?: number; ageMs?: number } = {}) {
  window.localStorage.setItem(KEY, JSON.stringify({ v, savedAt: Date.now() - ageMs, data }));
}

async function flushMicrotasks() {
  await act(async () => {
    await Promise.resolve();
  });
}

describe("useLocalStorageDraft", () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("starts from the default when nothing is stored", () => {
    const { result } = renderHook(() => useLocalStorageDraft(KEY, "initial", { version: 1 }));
    expect(result.current.value).toBe("initial");
    expect(result.current.restored).toBe(false);
    expect(result.current.noticeVisible).toBe(false);
  });

  it("restores a valid draft and shows the notice", async () => {
    seed("drafted text");
    const { result } = renderHook(() => useLocalStorageDraft(KEY, "initial", { version: 1 }));

    await flushMicrotasks();

    expect(result.current.value).toBe("drafted text");
    expect(result.current.restored).toBe(true);
    expect(result.current.noticeVisible).toBe(true);
    expect(result.current.restoredAt).toBeInstanceOf(Date);
  });

  it("discards a draft with a mismatched version", async () => {
    seed("old shape", { v: 99 });
    const { result } = renderHook(() => useLocalStorageDraft(KEY, "initial", { version: 1 }));

    await flushMicrotasks();

    expect(result.current.value).toBe("initial");
    expect(result.current.restored).toBe(false);
    expect(window.localStorage.getItem(KEY)).toBeNull();
  });

  it("discards an expired draft", async () => {
    seed("stale", { ageMs: 10_000 });
    const { result } = renderHook(() =>
      useLocalStorageDraft(KEY, "initial", { version: 1, ttlMs: 5_000 }),
    );

    await flushMicrotasks();

    expect(result.current.value).toBe("initial");
    expect(window.localStorage.getItem(KEY)).toBeNull();
  });

  it("discards a corrupt entry without throwing", async () => {
    window.localStorage.setItem(KEY, "{not json");
    const { result } = renderHook(() => useLocalStorageDraft(KEY, "initial", { version: 1 }));

    await flushMicrotasks();

    expect(result.current.value).toBe("initial");
    expect(window.localStorage.getItem(KEY)).toBeNull();
  });

  it("applies transformOnRestore to the restored data", async () => {
    seed({ step: 2, name: "drafted" });
    const { result } = renderHook(() =>
      useLocalStorageDraft(
        KEY,
        { step: 1, name: "" },
        { version: 1, transformOnRestore: (d) => ({ ...d, step: 5 }) },
      ),
    );

    await flushMicrotasks();

    expect(result.current.value).toEqual({ step: 5, name: "drafted" });
  });

  it("persists after the debounce window", async () => {
    const { result } = renderHook(() =>
      useLocalStorageDraft(KEY, "", { version: 1, debounceMs: 500 }),
    );

    act(() => result.current.setValue("typed"));
    expect(window.localStorage.getItem(KEY)).toBeNull();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(500);
    });

    const stored = JSON.parse(window.localStorage.getItem(KEY)!);
    expect(stored.data).toBe("typed");
    expect(stored.v).toBe(1);
  });

  it("does not persist pristine state when isDefault matches", async () => {
    const { result } = renderHook(() =>
      useLocalStorageDraft(KEY, "", {
        version: 1,
        debounceMs: 100,
        isDefault: (v) => v.trim() === "",
      }),
    );

    act(() => result.current.setValue("   "));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });

    expect(window.localStorage.getItem(KEY)).toBeNull();
  });

  it("flushes the debounce buffer on pagehide", async () => {
    const { result } = renderHook(() =>
      useLocalStorageDraft(KEY, "", { version: 1, debounceMs: 5_000 }),
    );

    act(() => result.current.setValue("about to navigate away"));
    expect(window.localStorage.getItem(KEY)).toBeNull();

    act(() => {
      window.dispatchEvent(new Event("pagehide"));
    });

    const stored = JSON.parse(window.localStorage.getItem(KEY)!);
    expect(stored.data).toBe("about to navigate away");
  });

  it("clearDraft removes the entry and cancels pending writes", async () => {
    seed("existing");
    const { result } = renderHook(() =>
      useLocalStorageDraft(KEY, "", { version: 1, debounceMs: 500 }),
    );
    await flushMicrotasks();

    act(() => result.current.setValue("editing more"));
    act(() => result.current.clearDraft());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1_000);
    });

    expect(window.localStorage.getItem(KEY)).toBeNull();
    expect(result.current.noticeVisible).toBe(false);
  });

  it("dismissNotice hides the notice but keeps the draft", async () => {
    seed("kept");
    const { result } = renderHook(() => useLocalStorageDraft(KEY, "", { version: 1 }));
    await flushMicrotasks();

    act(() => result.current.dismissNotice());

    expect(result.current.noticeVisible).toBe(false);
    expect(result.current.value).toBe("kept");
    expect(window.localStorage.getItem(KEY)).not.toBeNull();
  });
});
