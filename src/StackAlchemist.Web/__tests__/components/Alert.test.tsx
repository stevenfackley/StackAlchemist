import { render, screen } from "@testing-library/react";
import { Alert } from "@/components/ui/alert";

describe("Alert", () => {
  it("renders error variant with role=alert", () => {
    render(<Alert variant="error">Something broke</Alert>);
    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Something broke");
  });

  it.each(["success", "info", "warning"] as const)(
    "renders %s variant with role=status",
    (variant) => {
      render(<Alert variant={variant}>Message</Alert>);
      expect(screen.getByRole("status")).toHaveTextContent("Message");
    },
  );

  it("defaults to info", () => {
    render(<Alert>Default</Alert>);
    expect(screen.getByRole("status")).toBeInTheDocument();
  });

  it("suppresses the icon when icon={false}", () => {
    const { container } = render(
      <Alert variant="error" icon={false}>
        No icon
      </Alert>,
    );
    expect(container.querySelector("svg")).toBeNull();
  });

  it("renders a custom icon node", () => {
    render(
      <Alert variant="info" icon={<span data-testid="custom-icon" />}>
        Custom
      </Alert>,
    );
    expect(screen.getByTestId("custom-icon")).toBeInTheDocument();
  });

  it("forwards data-testid and className", () => {
    render(
      <Alert variant="warning" data-testid="my-alert" className="extra-class">
        Hi
      </Alert>,
    );
    expect(screen.getByTestId("my-alert")).toHaveClass("extra-class");
  });
});
