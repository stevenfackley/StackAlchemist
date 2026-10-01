/**
 * Without this file Next.js falls back to its built-in PostCSS pipeline, which does
 * not know about Tailwind: the stylesheet's Tailwind directives were emitted verbatim
 * into the production CSS bundle and every `className="text-2xl …"` in the app was a
 * no-op. Tailwind is a declared dependency of this project — this is what makes it run.
 * Tailwind 4 ships its PostCSS plugin as `@tailwindcss/postcss` and handles vendor
 * prefixing itself, so autoprefixer is no longer needed.
 */
const config = {
  plugins: {
    "@tailwindcss/postcss": {},
  },
};

export default config;
