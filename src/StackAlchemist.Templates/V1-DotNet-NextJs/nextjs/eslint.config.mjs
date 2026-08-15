import { dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { FlatCompat } from "@eslint/eslintrc";

const compat = new FlatCompat({
  baseDirectory: dirname(fileURLToPath(import.meta.url)),
});

// `next build` skips linting entirely when no config is present, and `npm run lint`
// (a script this project ships) has nothing to run — so the whole eslint-config-next
// devDependency was dead weight. This is the flat config create-next-app emits.
const config = [
  ...compat.extends("next/core-web-vitals", "next/typescript"),
  {
    ignores: [".next/**", "node_modules/**", "next-env.d.ts"],
  },
];

export default config;
