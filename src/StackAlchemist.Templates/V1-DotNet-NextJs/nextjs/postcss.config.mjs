/**
 * Without this file Next.js falls back to its built-in PostCSS pipeline, which does
 * not know about Tailwind: `@tailwind base/components/utilities` was emitted verbatim
 * into the production CSS bundle and every `className="text-2xl …"` in the app was a
 * no-op. Tailwind is a declared dependency of this project — this is what makes it run.
 */
const config = {
  plugins: {
    tailwindcss: {},
    autoprefixer: {},
  },
};

export default config;
