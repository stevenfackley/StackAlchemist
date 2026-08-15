import next from "eslint-config-next/core-web-vitals";
import nextTypeScript from "eslint-config-next/typescript";

// `next build` skips linting entirely when no config is present, and `npm run lint`
// (a script this project ships) has nothing to run — so the whole eslint-config-next
// devDependency would be dead weight without this file.
//
// eslint-config-next 16 ships native flat configs, so both entry points are imported
// directly. The `@eslint/eslintrc` FlatCompat shim the Next 15 version required now
// dies with "TypeError: Converting circular structure to JSON" — the eslintrc validator
// cannot serialise a flat config's plugin objects. Each entry point already declares the
// standard `.next/**`, `out/**`, `build/**` and `next-env.d.ts` global ignores.
const config = [...next, ...nextTypeScript];

export default config;
