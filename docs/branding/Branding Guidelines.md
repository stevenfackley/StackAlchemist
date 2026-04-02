# StackAlchemist: Branding & Style Guidelines (v1.0)

## 🎨 Color Palette

The StackAlchemist palette is designed for high-impact dark mode, emphasizing "energy" and "precision."

| Name           | Hex Code   | Usage                                      | Tailwind Class         |
| :------------- | :--------- | :----------------------------------------- | :--------------------- |
| **Deep Void**  | `#0F172A`  | Primary Background / Hexagon Base          | `bg-slate-950`         |
| **Slate Gray** | `#1E293B`  | Secondary Background / Component Cards     | `bg-slate-800`         |
| **Electric Blue**| `#3B82F6`| Primary Brand Color / Icons / Borders      | `text-blue-500`        |
| **Emerald**    | `#10B981`  | Success / Build Pass / Active Generation   | `text-emerald-500`     |
| **Rose**       | `#F43F5E`  | Error / Build Fail / Critical Warnings     | `text-rose-500`        |
| **Pure White** | `#FFFFFF`  | Primary Text / High Contrast Elements      | `text-white`           |

---

## 💎 Visual Identity

### The Logo (Hexagon + Stack + Star)
The logo symbolizes the transmutation of raw code (the stack) into a structured, crystalline whole (the hexagon), activated by the AI "spark" (the star).

- **Hexagon:** Represents structure and the alchemical "Great Work."
- **Triple Stack:** Represents the three tiers of modern software: Database, Backend, Frontend.
- **The Alchemist's Star:** A four-point diamond star representing the AI's intelligent synthesis.

### Favicon
The favicon is a high-visibility version of the logo, featuring the **Electric Blue** Alchemist's Star centered within the **Deep Void** Hexagon.

---

## 🔡 Typography

We prioritize readability and a "technical elite" aesthetic.

- **Primary Sans:** [Inter](https://rsms.me/inter/)
  - *Usage:* UI, Body text, Headers.
- **Primary Mono:** [JetBrains Mono](https://www.jetbrains.com/lp/mono/)
  - *Usage:* Code blocks, the "Simple Mode" terminal prompt, generation logs.

---

## 🗣️ Voice & Tone

StackAlchemist speaks as a **Senior Staff Engineer** who has seen it all and has no time for fluff.

- **Direct:** Use imperative language ("Transmute", "Execute", "Deploy").
- **Expert:** Avoid marketing jargon. Use specific technical terms (RLS, Dapper, Micro ORM, IaC).
- **Alchemical Narrative:** Use subtle mystical analogies for technical processes (e.g., "Synthesizing Architecture", "The Cauldron", "Transmutation Complete").
- **Professional:** Clear, objective, and authoritative.

---

## 📐 UI Components

- **Library:** `shadcn/ui` (Radix UI + Tailwind).
- **Radius:** Small (`4px`) or Sharp (`0px`). Avoid large rounded corners.
- **Borders:** Thin (`1px`) Electric Blue or Slate Gray borders only. No heavy shadows.
- **Glassmorphism:** Use subtle backdrop blurs (`backdrop-blur-md`) on modal overlays and floating toolbars.
