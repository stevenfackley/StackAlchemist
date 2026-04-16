import { ImageResponse } from "next/og";

// Dynamic OG image rendered via Satori (Next.js `ImageResponse`).
// Replaces the 866 KB static og-image.png with a ~60 KB render that
// caches at the edge and can be overridden per-route by adding another
// opengraph-image.tsx inside any route segment.

export const runtime = "edge";
export const alt = "StackAlchemist — AI SaaS Generator with a Compile Guarantee";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

const VOID = "#0f172a";
const VOID_ACCENT = "#1e293b";
const ELECTRIC = "#4da6ff";
const EMERALD = "#10b981";
const SLATE = "#94a3b8";

export default function OgImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          padding: "64px",
          background: `linear-gradient(135deg, ${VOID} 0%, ${VOID_ACCENT} 100%)`,
          color: "#ffffff",
          fontFamily: "system-ui, -apple-system, Segoe UI, Roboto, sans-serif",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 20 }}>
          <div
            style={{
              width: 52,
              height: 52,
              borderRadius: 8,
              background: ELECTRIC,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 30,
              fontWeight: 800,
              color: VOID,
            }}
          >
            SA
          </div>
          <span
            style={{
              fontSize: 28,
              fontWeight: 700,
              letterSpacing: -0.5,
            }}
          >
            StackAlchemist
          </span>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
          <div
            style={{
              fontSize: 20,
              letterSpacing: 6,
              textTransform: "uppercase",
              color: ELECTRIC,
              fontWeight: 600,
            }}
          >
            AI SaaS Generator
          </div>
          <div
            style={{
              fontSize: 84,
              fontWeight: 800,
              lineHeight: 1.05,
              letterSpacing: -2,
              maxWidth: 1000,
            }}
          >
            Synthesize your platform from natural language.
          </div>
          <div style={{ fontSize: 28, color: SLATE, maxWidth: 900, lineHeight: 1.35 }}>
            100% compile guarantee. .NET 10 + Next.js 15 + PostgreSQL repositories you own.
          </div>
        </div>

        <div style={{ display: "flex", gap: 32, alignItems: "center" }}>
          <div
            style={{
              fontSize: 18,
              fontWeight: 600,
              padding: "10px 16px",
              borderRadius: 4,
              background: EMERALD,
              color: VOID,
              letterSpacing: 1,
              textTransform: "uppercase",
            }}
          >
            Own it forever
          </div>
          <span style={{ fontSize: 22, color: SLATE }}>stackalchemist.app</span>
        </div>
      </div>
    ),
    { ...size }
  );
}
