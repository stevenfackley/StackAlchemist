import { access } from "node:fs/promises";
import { constants } from "node:fs";
import { resolve } from "node:path";
import { spawnSync } from "node:child_process";

const target = resolve(process.cwd(), "../../scripts/setup-env.mjs");

try {
  await access(target, constants.F_OK);
  const result = spawnSync(process.execPath, [target], { stdio: "inherit" });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
} catch {
  console.warn("[setup-env-safe] Skipping repo-level env bootstrap.");
}