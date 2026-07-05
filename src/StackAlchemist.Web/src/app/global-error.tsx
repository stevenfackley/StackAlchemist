"use client";

import { useEffect } from "react";

/**
 * Root-layout crash boundary. Replaces the entire root layout when layout.tsx
 * itself throws, so it must render its own <html>/<body> and cannot rely on
 * globals.css — all styling is inline.
 */
export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <html lang="en">
      <body
        style={{
          margin: 0,
          minHeight: "100vh",
          display: "grid",
          placeItems: "center",
          background: "#1e293b",
          color: "#f1f5f9",
          fontFamily: "ui-sans-serif, system-ui, sans-serif",
          padding: "16px",
        }}
      >
        <div
          style={{
            maxWidth: 448,
            width: "100%",
            borderRadius: 16,
            border: "1px solid rgba(71, 85, 105, 0.3)",
            background: "rgba(15, 23, 42, 0.6)",
            padding: 24,
            textAlign: "center",
          }}
        >
          <p
            style={{
              fontFamily: "ui-monospace, monospace",
              fontSize: 12,
              letterSpacing: "0.2em",
              textTransform: "uppercase",
              color: "#64748b",
              margin: "0 0 16px",
            }}
          >
            Stack Alchemist
          </p>
          <h1 style={{ fontSize: 24, fontWeight: 700, color: "#ffffff", margin: "0 0 16px" }}>
            Something went wrong
          </h1>
          <p style={{ fontSize: 14, color: "#94a3b8", lineHeight: 1.6, margin: "0 0 16px" }}>
            An unexpected error prevented the page from loading. Reloading usually fixes it.
          </p>
          {error.digest && (
            <p
              style={{
                fontFamily: "ui-monospace, monospace",
                fontSize: 12,
                color: "#64748b",
                margin: "0 0 16px",
              }}
            >
              Reference: {error.digest}
            </p>
          )}
          <button
            type="button"
            onClick={reset}
            style={{
              borderRadius: 9999,
              border: "1px solid rgba(77, 166, 255, 0.4)",
              background: "rgba(77, 166, 255, 0.1)",
              color: "#4da6ff",
              padding: "8px 16px",
              fontFamily: "ui-monospace, monospace",
              fontSize: 12,
              letterSpacing: "0.2em",
              textTransform: "uppercase",
              cursor: "pointer",
            }}
          >
            Reload
          </button>
        </div>
      </body>
    </html>
  );
}
