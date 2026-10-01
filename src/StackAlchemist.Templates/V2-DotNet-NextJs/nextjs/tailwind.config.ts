import type { Config } from "tailwindcss";

// Tailwind 4 is configured in CSS (src/app/globals.css) and finds class names on its own, so
// this file carries only theme extensions — the personalized palette goes in `extend.colors`.
// globals.css loads it with `@config`.
const config: Config = {
  theme: {
    extend: {},
  },
};

export default config;
