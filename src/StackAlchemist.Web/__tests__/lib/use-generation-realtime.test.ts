import { renderHook, act, waitFor } from "@testing-library/react";
import { useGenerationRealtime } from "@/lib/hooks/use-generation-realtime";

// Mutable holder so individual tests can swap the client (or null it out).
const supabaseHolder: { client: unknown } = { client: null };

vi.mock("@/lib/supabase", () => ({
  get supabase() {
    return supabaseHolder.client;
  },
}));

const getGenerationMock = vi.fn();
vi.mock("@/lib/actions", () => ({
  getGeneration: (...args: unknown[]) => getGenerationMock(...args),
}));

type SubscribeCallback = (status: string) => void;
type ChangeHandler = (payload: { new: unknown }) => void;

function makeFakeClient() {
  const channels: Array<{
    name: string;
    changeHandler?: ChangeHandler;
    subscribeCallback?: SubscribeCallback;
  }> = [];
  const removed: string[] = [];

  const client = {
    channel(name: string) {
      const entry: (typeof channels)[number] = { name };
      channels.push(entry);
      const chan = {
        _name: name,
        on(_event: string, _filter: unknown, handler: ChangeHandler) {
          entry.changeHandler = handler;
          return chan;
        },
        subscribe(cb: SubscribeCallback) {
          entry.subscribeCallback = cb;
          return chan;
        },
      };
      return chan;
    },
    removeChannel(chan: { _name: string }) {
      removed.push(chan._name);
      return Promise.resolve("ok");
    },
  };

  return { client, channels, removed };
}

describe("useGenerationRealtime", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getGenerationMock.mockReset();
    getGenerationMock.mockResolvedValue(null);
    supabaseHolder.client = null;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("is off when disabled or without a generation id", () => {
    const { result } = renderHook(() =>
      useGenerationRealtime({ generationId: null, onUpdate: vi.fn() }),
    );
    expect(result.current.transport).toBe("off");
    expect(getGenerationMock).not.toHaveBeenCalled();
  });

  it("falls back to polling-only when the supabase client is null", async () => {
    const onUpdate = vi.fn();
    const row = { id: "g1", status: "building" };
    getGenerationMock.mockResolvedValue(row);

    const { result } = renderHook(() =>
      useGenerationRealtime({ generationId: "g1", onUpdate, pollMs: 1000 }),
    );

    expect(result.current.transport).toBe("polling");
    // Immediate catch-up fetch + one poll tick.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    expect(getGenerationMock).toHaveBeenCalled();
    expect(onUpdate).toHaveBeenCalledWith(row);
  });

  it("runs a catch-up fetch once SUBSCRIBED and reports realtime transport", async () => {
    const fake = makeFakeClient();
    supabaseHolder.client = fake.client;
    const onUpdate = vi.fn();
    const row = { id: "g2", status: "success" };
    getGenerationMock.mockResolvedValue(row);

    const { result } = renderHook(() =>
      useGenerationRealtime({ generationId: "g2", onUpdate }),
    );

    expect(fake.channels).toHaveLength(1);
    expect(fake.channels[0].name).toBe("gen:g2");

    await act(async () => {
      fake.channels[0].subscribeCallback!("SUBSCRIBED");
      await vi.runOnlyPendingTimersAsync();
    });

    expect(result.current.transport).toBe("realtime");
    expect(getGenerationMock).toHaveBeenCalledWith("g2");
    expect(onUpdate).toHaveBeenCalledWith(row);
  });

  it("delivers live postgres_changes payloads to onUpdate", async () => {
    const fake = makeFakeClient();
    supabaseHolder.client = fake.client;
    const onUpdate = vi.fn();

    renderHook(() => useGenerationRealtime({ generationId: "g3", onUpdate }));

    const row = { id: "g3", status: "packing" };
    act(() => {
      fake.channels[0].changeHandler!({ new: row });
    });

    expect(onUpdate).toHaveBeenCalledWith(row);
  });

  it("starts polling and schedules a re-subscribe on CHANNEL_ERROR", async () => {
    const fake = makeFakeClient();
    supabaseHolder.client = fake.client;
    const onUpdate = vi.fn();
    getGenerationMock.mockResolvedValue({ id: "g4", status: "building" });

    const { result } = renderHook(() =>
      useGenerationRealtime({ generationId: "g4", onUpdate, pollMs: 5000 }),
    );

    await act(async () => {
      fake.channels[0].subscribeCallback!("CHANNEL_ERROR");
    });
    expect(result.current.transport).toBe("polling");

    // Poll tick fires while down.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(5000);
    });
    expect(getGenerationMock).toHaveBeenCalled();

    // Backoff elapses → a fresh channel is created.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000);
    });
    expect(fake.channels.length).toBeGreaterThanOrEqual(2);

    // Recovery flips back to realtime and stops the poller.
    getGenerationMock.mockClear();
    await act(async () => {
      fake.channels[fake.channels.length - 1].subscribeCallback!("SUBSCRIBED");
      await vi.runOnlyPendingTimersAsync();
    });
    expect(result.current.transport).toBe("realtime");
    getGenerationMock.mockClear();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000);
    });
    expect(getGenerationMock).not.toHaveBeenCalled();
  });

  it("stops all activity after unmount", async () => {
    const fake = makeFakeClient();
    supabaseHolder.client = fake.client;
    const onUpdate = vi.fn();

    const { unmount } = renderHook(() =>
      useGenerationRealtime({ generationId: "g5", onUpdate, pollMs: 1000 }),
    );

    unmount();
    expect(fake.removed).toContain("gen:g5");

    await act(async () => {
      await vi.advanceTimersByTimeAsync(10_000);
    });
    expect(onUpdate).not.toHaveBeenCalled();
  });
});
