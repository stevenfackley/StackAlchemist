import type { NextConfig } from "next";
import { execSync } from "child_process";

// Capture the last git commit date at build time so the test-site banner
// can display it without any runtime overhead.
function getGitCommitDate(): string {
  try {
    return execSync("git log -1 --format=%ci", { encoding: "utf8" }).trim();
  } catch {
    return new Date().toISOString();
  }
}

const nextConfig: NextConfig = {
  env: {
    NEXT_PUBLIC_BUILD_DATE: getGitCommitDate(),
  },
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
