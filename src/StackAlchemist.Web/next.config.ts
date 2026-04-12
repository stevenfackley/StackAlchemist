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
  images: {
    formats: ["image/avif", "image/webp"],
  },
  output: "standalone",
  outputFileTracingRoot: __dirname,
  webpack: (config) => {
    // Fix Windows + pnpm symlink casing collision:
    // pnpm uses a virtual store with symlinks; webpack follows those symlinks and
    // ends up with both "C:\..." and "c:\..." paths for the same file, causing
    // next/dist/pages/_document to load twice and breaking the <Html> singleton
    // check during static prerendering of /404.
    // Setting symlinks:false makes webpack use the symlink path as-is (consistent
    // casing) rather than resolving through to the virtual store.
    config.resolve.symlinks = false;
    return config;
  },
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
