import type { Config } from "tailwindcss";

// Tailwind 4 is configured in CSS (src/index.css) and finds class names on its own, so this
// file carries only theme extensions — the personalized palette goes in `extend.colors`.
// src/index.css loads it with `@config`.
export default {
  theme: { extend: {} },
} satisfies Config;
