import path from "node:path";
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  // `output: "standalone"` traces runtime files from an inferred workspace root. This app
  // lives in a subdirectory of the archive, so Next walks up, finds whatever lockfile is
  // above it, and roots the standalone bundle there — producing a server.js buried under a
  // mirror of the host's absolute path. Pinning the root to this directory keeps the
  // standalone output at .next/standalone/server.js, which is what the Dockerfile copies.
  outputFileTracingRoot: path.join(__dirname),
  env: {
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000",
    NEXT_PUBLIC_SUPABASE_URL: process.env.NEXT_PUBLIC_SUPABASE_URL ?? "",
    NEXT_PUBLIC_SUPABASE_ANON_KEY: process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY ?? "",
  },
};

export default nextConfig;
