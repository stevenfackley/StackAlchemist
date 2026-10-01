/**
 * Runs Tailwind over the app's stylesheet. Without this file Next.js uses its built-in
 * PostCSS pipeline, which does not know about Tailwind, and every utility class in the app
 * is a no-op. Tailwind 4 ships its PostCSS plugin as `@tailwindcss/postcss` and handles
 * vendor prefixing itself.
 */
const config = {
  plugins: {
    "@tailwindcss/postcss": {},
  },
};

export default config;
