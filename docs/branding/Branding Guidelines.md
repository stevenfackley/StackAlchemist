# StackAlchemist: Branding & Style Guidelines (v1.1)

## Color Palette

The current StackAlchemist UI uses an elevated-slate dark theme rather than a near-black console theme. The look should feel technical, cinematic, and controlled, with bright electric accents used sparingly against layered slate surfaces.

| Name           | Hex Code   | Usage                                      | Tailwind Class         |
| :------------- | :--------- | :----------------------------------------- | :--------------------- |
| **Void Deep**  | `#0F172A`  | Deepest shadows, grid contrast, scrollbar track | `bg-void-deep`     |
| **Elevated Void** | `#1E293B` | Primary page background and large layout fields | `bg-void`         |
| **Slate Surface** | `#334155` | Cards, dividers, secondary surfaces, chips | `bg-slate-surface` |
| **Slate Panel** | `#475569` | Higher-emphasis panels, stronger borders    | `bg-slate-panel`       |
| **Electric Blue**| `#4DA6FF`| Primary brand accent, active states, glows, feature emphasis | `text-electric` |
| **Emerald**    | `#10B981`  | Success / Build Pass / Active Generation   | `text-emerald-500`     |
| **Rose**       | `#F43F5E`  | Error / Build Fail / Critical Warnings     | `text-rose-500`        |
| **Pure White** | `#FFFFFF`  | Primary Text / High Contrast Elements      | `text-white`           |

---

## Visual Identity

### Current UI Direction
- The home page now opens with a full-height hero focused on positioning copy, followed by a dedicated "Launch Console" section below the fold.
- The primary CTA surface is a terminal-inspired prompt card with a builder layer, not a plain textarea.
- Pricing, simple mode, and advanced mode should all reuse the same brand cues: elevated slate backgrounds, electric-blue accents, mono utility text, thin borders, and restrained glow.
- The top logo should always function as a clear path back to the home screen on top-level marketing pages.
- The hero should feel cinematic and uncluttered on desktop. Do not crowd the opening viewport with too many equal-weight cards.
- Mobile layouts should stack cleanly: hero copy first, handoff summary second, launch console after that.

### The Logo (Hexagon + Stack + Star)
The logo symbolizes the transmutation of raw code (the stack) into a structured, crystalline whole (the hexagon), activated by the AI "spark" (the star).

- **Hexagon:** Represents structure and the alchemical "Great Work."
- **Triple Stack:** Represents the three tiers of modern software: Database, Backend, Frontend.
- **The Alchemist's Star:** A four-point diamond star representing the AI's intelligent synthesis.

### Favicon
The favicon is a high-visibility version of the logo, featuring the **Electric Blue** Alchemist's Star centered within the **Deep Void** Hexagon.

---

## Typography

We prioritize readability and a "technical elite" aesthetic.

- **Primary Sans:** [Inter](https://rsms.me/inter/)
  - *Usage:* UI, Body text, Headers.
- **Primary Mono:** [JetBrains Mono](https://www.jetbrains.com/lp/mono/)
  - *Usage:* Code blocks, the "Simple Mode" terminal prompt, generation logs.

---

## Voice & Tone

StackAlchemist speaks as a **Senior Staff Engineer** who has seen it all and has no time for fluff.

- **Direct:** Use imperative language ("Transmute", "Execute", "Deploy").
- **Expert:** Avoid marketing jargon. Use specific technical terms (RLS, Dapper, Micro ORM, IaC).
- **Alchemical Narrative:** Use subtle mystical analogies for technical processes (e.g., "Synthesizing Architecture", "The Cauldron", "Transmutation Complete").
- **Professional:** Clear, objective, and authoritative.

---

## UI Components

- **Library:** `shadcn/ui` (Radix UI + Tailwind).
- **Radius:** Small utility radii are still valid, but large marketing panels now use softer radii around `24px` to `28px`. Do not flatten those panels back to sharp corners.
- **Borders:** Thin (`1px`) slate or electric borders only. Use electric borders for active emphasis, not everywhere.
- **Glassmorphism:** Use subtle backdrop blurs (`backdrop-blur-md`) on modal overlays, sticky nav, and elevated action surfaces.
- **Home Hero Guidance:** Let the opening hero own the first screen. The prompt console belongs in its own section immediately below, with supporting delivery and flow content attached to that console rather than to the hero.
- **Prompt Builder Guidance:** Builder chips should create a useful brief quickly. They should feel assistive, not noisy.
