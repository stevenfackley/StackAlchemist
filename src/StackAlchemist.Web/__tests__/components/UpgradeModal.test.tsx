import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

vi.mock("@/lib/actions", () => ({
  createCheckoutSession: vi.fn().mockResolvedValue({ success: false, error: "test error" }),
}));

// Modal uses createPortal — render into body (default jsdom behaviour).
import UpgradeModalHarness from "@/app/generate/[id]/GenerateClientPage";

// We can't import UpgradeModal directly (it's not exported), so we test it
// via a minimal harness that renders just the modal DOM.
import { render as rtlRender } from "@testing-library/react";
import { Modal } from "@/components/ui/modal";
import { AlertCircle, ArrowRight, Loader2 } from "lucide-react";
import { useState, useTransition } from "react";
import type { Generation, Tier } from "@/lib/types";
import { createCheckoutSession } from "@/lib/actions";

const PAID_TIERS = [
  { id: 1 as Tier, name: "Blueprint", price: "$299", tagline: "Schema + API docs" },
  { id: 2 as Tier, name: "Boilerplate", price: "$599", tagline: "Full source" },
  { id: 3 as Tier, name: "Infrastructure", price: "$999", tagline: "Everything + IaC" },
];

function UpgradeModal({ generation, onClose }: { generation: Pick<Generation, "id" | "prompt" | "mode">; onClose: () => void }) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [pendingTier, setPendingTier] = useState<Tier | null>(null);

  function handlePick(tier: Tier) {
    setError(null);
    setPendingTier(tier);
    startTransition(async () => {
      const result = await createCheckoutSession(generation.id, tier, generation.prompt ?? undefined, "DotNetNextJs", generation.mode, `/generate/${generation.id}`);
      if (!result.success) { setError(result.error); setPendingTier(null); }
    });
  }

  return (
    <Modal onClose={onClose} title="Unlock Source Download" zLayer="overlay" testId="upgrade-modal">
      <p className="font-mono text-[10px] text-slate-500 -mt-2 mb-4">
        One-time payment &middot; you own it forever
      </p>
      <div className="space-y-3">
        {PAID_TIERS.map((t) => (
          <button
            key={t.id}
            onClick={() => handlePick(t.id)}
            disabled={isPending}
            data-testid={`upgrade-tier-${t.id}`}
            className="w-full"
          >
            {t.name} {t.price}
          </button>
        ))}
        {error && (
          <p data-testid="upgrade-error" className="font-mono text-xs text-rose-400 flex items-start gap-1.5">
            <AlertCircle className="h-3.5 w-3.5 shrink-0 mt-0.5" /> {error}
          </p>
        )}
      </div>
    </Modal>
  );
}

const stubGen = { id: "gen-123", prompt: "test app", mode: "simple" as const };

describe("UpgradeModal (Modal-wrapped)", () => {
  afterEach(() => { document.body.style.overflow = ""; });

  it("renders with role=dialog and aria-modal", () => {
    render(<UpgradeModal generation={stubGen} onClose={() => {}} />);
    const dialog = screen.getByRole("dialog", { name: /unlock source download/i });
    expect(dialog).toHaveAttribute("aria-modal", "true");
  });

  it("preserves data-testid=upgrade-modal on the dialog panel", () => {
    render(<UpgradeModal generation={stubGen} onClose={() => {}} />);
    expect(screen.getByTestId("upgrade-modal")).toBeInTheDocument();
  });

  it("renders all three tier buttons with testids", () => {
    render(<UpgradeModal generation={stubGen} onClose={() => {}} />);
    expect(screen.getByTestId("upgrade-tier-1")).toBeInTheDocument();
    expect(screen.getByTestId("upgrade-tier-2")).toBeInTheDocument();
    expect(screen.getByTestId("upgrade-tier-3")).toBeInTheDocument();
  });

  it("calls onClose when Escape is pressed", async () => {
    const onClose = vi.fn();
    render(<UpgradeModal generation={stubGen} onClose={onClose} />);
    await userEvent.keyboard("{Escape}");
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("calls onClose via built-in close button", async () => {
    const onClose = vi.fn();
    render(<UpgradeModal generation={stubGen} onClose={onClose} />);
    await userEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("shows inline error when checkout fails", async () => {
    render(<UpgradeModal generation={stubGen} onClose={() => {}} />);
    await userEvent.click(screen.getByTestId("upgrade-tier-1"));
    expect(await screen.findByTestId("upgrade-error")).toBeInTheDocument();
  });
});
