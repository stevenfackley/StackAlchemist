import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./src/app/**/*.{ts,tsx}",
    "./src/components/**/*.{ts,tsx}",
    "./src/lib/**/*.{ts,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        /* Main backgrounds - elevated slate palette matching v0 */
        void: "#1e293b",
        "void-deep": "#0f172a",
        /* Card/panel surfaces */
        "slate-surface": "#334155",
        "slate-panel": "#475569",
        /* Accents */
        electric: "#4da6ff",
        neon: "#4da6ff",
        emerald: "#10b981",
        rose: "#f43f5e",
        /* Borders */
        "slate-border": "#475569",
      },
      fontFamily: {
        sans: ["Inter", "ui-sans-serif", "system-ui", "sans-serif"],
        mono: ["JetBrains Mono", "ui-monospace", "monospace"],
      },
      borderRadius: {
        sm: "4px",
        none: "0px",
        xl: "12px",
        "2xl": "16px",
      },
      animation: {
        "pulse-slow": "pulse-slow 8s ease-in-out infinite",
        "pulse-slower": "pulse-slow 12s ease-in-out infinite",
        "pulse-glow": "pulse-glow 4s ease-in-out infinite",
        float: "float 6s ease-in-out infinite",
      },
      keyframes: {
        "pulse-slow": {
          "0%, 100%": { opacity: "1", transform: "scale(1)" },
          "50%": { opacity: "0.5", transform: "scale(1.08)" },
        },
        "pulse-glow": {
          "0%, 100%": { opacity: "0.3", transform: "scale(1)" },
          "50%": { opacity: "0.5", transform: "scale(1.05)" },
        },
        float: {
          "0%, 100%": { transform: "translateY(0px)" },
          "50%": { transform: "translateY(-10px)" },
        },
      },
    },
  },
  plugins: [require("tailwindcss-animate")],
};

export default config;
