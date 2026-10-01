import { assertAuthModeConsistent } from "@/lib/runtime-config";

/** Runs once per server start (Next instrumentation hook). Fail fast on an impossible mode. */
export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") assertAuthModeConsistent();
}
