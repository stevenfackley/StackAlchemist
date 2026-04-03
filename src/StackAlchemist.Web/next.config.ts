import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  outputFileTracingRoot: __dirname,
  async headers() {
    return [
      {
        // StackBlitz WebContainers requires cross-origin isolation (SharedArrayBuffer).
        // Apply COOP + COEP headers to the generate result page only.
        source: "/generate/:id*",
        headers: [
          {
            key: "Cross-Origin-Opener-Policy",
            value: "same-origin",
          },
          {
            key: "Cross-Origin-Embedder-Policy",
            value: "require-corp",
          },
        ],
      },
    ];
  },
};

export default nextConfig;
