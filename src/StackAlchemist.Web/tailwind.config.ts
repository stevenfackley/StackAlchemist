import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./src/app/**/*.{ts,tsx}",
    "./src/components/**/*.{ts,tsx}",
    "./src/lib/**/*.{ts,tsx}",
  ],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        void: "#0F172A",
        "slate-surface": "#1E293B",
        electric: "#3B82F6",
        emerald: "#10B981",
        rose: "#F43F5E",
        "slate-border": "#334155",
      },
      fontFamily: {
        sans: ["Inter", "ui-sans-serif", "system-ui", "sans-serif"],
        mono: ["JetBrains Mono", "ui-monospace", "monospace"],
      },
      borderRadius: {
        sm: "4px",
        none: "0px",
      },
    },
  },
  plugins: [require("tailwindcss-animate")],
};

export default config;
