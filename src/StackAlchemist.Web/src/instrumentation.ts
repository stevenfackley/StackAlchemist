import { assertProductionConfig } from "@/lib/runtime-config";

/** Runs once per server start (Next instrumentation hook). Fail fast on a configuration production cannot run with. */
export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") assertProductionConfig();
}
