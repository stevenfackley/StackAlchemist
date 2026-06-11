import { render, screen, fireEvent } from "@testing-library/react";
import { useRef } from "react";
import { useDismissable } from "@/lib/hooks/use-dismissable";

function WithContainer({ onClose, enabled = true }: { onClose: () => void; enabled?: boolean }) {
  const containerRef = useRef<HTMLDivElement>(null);
  useDismissable(containerRef, onClose, enabled);
  return (
    <div>
      <div ref={containerRef} data-testid="container">
        <button>Inside</button>
      </div>
      <button data-testid="outside">Outside</button>
    </div>
  );
}

function EscOnly({ onClose, enabled = true }: { onClose: () => void; enabled?: boolean }) {
  useDismissable(null, onClose, enabled);
  return <div data-testid="esc-only">content</div>;
}

describe("useDismissable", () => {
  it("calls onClose on Escape", () => {
    const onClose = vi.fn();
    render(<WithContainer onClose={onClose} />);
    fireEvent.keyDown(document, { key: "Escape" });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("calls onClose on pointerdown outside the container", () => {
    const onClose = vi.fn();
    render(<WithContainer onClose={onClose} />);
    fireEvent.pointerDown(screen.getByTestId("outside"));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("does NOT call onClose on pointerdown inside the container", () => {
    const onClose = vi.fn();
    render(<WithContainer onClose={onClose} />);
    fireEvent.pointerDown(screen.getByText("Inside"));
    expect(onClose).not.toHaveBeenCalled();
  });

  it("does not fire when enabled=false", () => {
    const onClose = vi.fn();
    render(<WithContainer onClose={onClose} enabled={false} />);
    fireEvent.keyDown(document, { key: "Escape" });
    fireEvent.pointerDown(screen.getByTestId("outside"));
    expect(onClose).not.toHaveBeenCalled();
  });

  it("ESC-only mode (null ref): closes on Escape", () => {
    const onClose = vi.fn();
    render(<EscOnly onClose={onClose} />);
    fireEvent.keyDown(document, { key: "Escape" });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("ESC-only mode: does not throw or fire on outside pointerdown", () => {
    const onClose = vi.fn();
    render(
      <div>
        <EscOnly onClose={onClose} />
        <button data-testid="outer">outer</button>
      </div>
    );
    fireEvent.pointerDown(screen.getByTestId("outer"));
    expect(onClose).not.toHaveBeenCalled();
  });

  it("does not fire on non-Escape keys", () => {
    const onClose = vi.fn();
    render(<WithContainer onClose={onClose} />);
    fireEvent.keyDown(document, { key: "Enter" });
    fireEvent.keyDown(document, { key: "Tab" });
    expect(onClose).not.toHaveBeenCalled();
  });
});
