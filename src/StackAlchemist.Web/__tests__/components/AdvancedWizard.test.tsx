import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";

// ReactFlow needs real layout — stub it for jsdom.
vi.mock("@xyflow/react", () => ({
  ReactFlow: ({ children }: { children?: ReactNode }) => <div data-testid="reactflow-stub">{children}</div>,
  Background: () => null,
  Controls: () => null,
  addEdge: (edge: unknown, edges: unknown[]) => [...edges, edge],
  useNodesState: () => [[], vi.fn(), vi.fn()],
  useEdgesState: () => [[], vi.fn(), vi.fn()],
  BackgroundVariant: { Dots: "dots" },
}));
vi.mock("@xyflow/react/dist/style.css", () => ({}));

vi.mock("@/lib/actions", () => ({
  submitAdvancedGeneration: vi.fn(),
  createPendingGeneration: vi.fn(),
  createCheckoutSession: vi.fn(),
  getFreeQuotaStatus: vi.fn().mockResolvedValue({ used: 0, limit: 3, remaining: 3 }),
}));
vi.mock("@/lib/hooks/use-generation-realtime", () => ({
  useGenerationRealtime: () => ({ transport: "off" as const }),
}));
vi.mock("@/lib/supabase", () => ({ supabase: {} }));
vi.mock("@/lib/runtime-config", () => ({ isDemoMode: false }));

const searchParamsMock = vi.hoisted(() => ({ value: new URLSearchParams() }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => searchParamsMock.value,
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), refresh: vi.fn() }),
  usePathname: () => "/advanced",
}));

import AdvancedModePage from "@/app/advanced/AdvancedModePage";

const DRAFT_KEY = "sa:draft:advanced:v1";

function seedDraft(overrides: Record<string, unknown> = {}, { v = 1, ageMs = 0 } = {}) {
  const data = {
    entities: [
      {
        name: "Invoice",
        fields: [
          { name: "id", type: "UUID", pk: true },
          { name: "total", type: "Decimal", pk: false },
        ],
      },
    ],
    relationships: [],
    endpoints: [],
    projectType: "DotNetNextJs",
    tier: 2,
    personalization: {},
    step: 1,
    ...overrides,
  };
  window.localStorage.setItem(DRAFT_KEY, JSON.stringify({ v, savedAt: Date.now() - ageMs, data }));
}

describe("AdvancedModePage draft persistence", () => {
  beforeEach(() => {
    window.localStorage.clear();
    searchParamsMock.value = new URLSearchParams();
    window.history.replaceState(null, "", "/advanced");
  });

  it("renders the default entity with no draft and no notice", async () => {
    render(<AdvancedModePage />);

    expect(await screen.findByDisplayValue("Product")).toBeInTheDocument();
    expect(screen.queryByTestId("advanced-draft-restored")).not.toBeInTheDocument();
  });

  it("restores a stored draft and shows the dismissible notice", async () => {
    seedDraft();
    render(<AdvancedModePage />);

    expect(await screen.findByDisplayValue("Invoice")).toBeInTheDocument();
    expect(screen.getByTestId("advanced-draft-restored")).toBeInTheDocument();
  });

  it("start fresh resets the wizard and removes the stored draft", async () => {
    seedDraft();
    render(<AdvancedModePage />);
    await screen.findByTestId("advanced-draft-restored");

    await userEvent.click(screen.getByRole("button", { name: "start fresh" }));

    await waitFor(() => expect(screen.getByDisplayValue("Product")).toBeInTheDocument());
    expect(window.localStorage.getItem(DRAFT_KEY)).toBeNull();
  });

  it("explicit URL params beat the drafted step and tier", async () => {
    seedDraft({ step: 2, tier: 3 });
    searchParamsMock.value = new URLSearchParams("step=5&tier=1");
    render(<AdvancedModePage />);

    // Draft content restores…
    await screen.findByTestId("advanced-draft-restored");
    // …but the deep-linked step wins: step 5 (tier grid) is rendered.
    expect(await screen.findByTestId("advanced-step-5-tier-grid")).toBeInTheDocument();
  });

  it("edits are persisted to localStorage after the debounce", async () => {
    render(<AdvancedModePage />);
    const nameInput = await screen.findByDisplayValue("Product");

    await userEvent.clear(nameInput);
    await userEvent.type(nameInput, "Order");

    await waitFor(
      () => {
        const raw = window.localStorage.getItem(DRAFT_KEY);
        expect(raw).not.toBeNull();
        expect(JSON.parse(raw!).data.entities[0].name).toBe("Order");
      },
      { timeout: 3000 },
    );
  });

  it("step navigation is mirrored into the URL", async () => {
    render(<AdvancedModePage />);
    await screen.findByDisplayValue("Product");

    await waitFor(() => {
      const url = new URL(window.location.href);
      expect(url.searchParams.get("step")).toBe("1");
    });
  });
});
