/**
 * The one allowed spacing scale (8-pt, with 4/2 for micro). Every gap/pad in the
 * workspace resolves to one of these — no ad-hoc `gap-2.5` / `space-y-5` / `[10px]`.
 * Tailwind step in comments.
 */
export type Gap = "2xs" | "xs" | "sm" | "md" | "lg" | "xl" | "2xl";

export const GAP: Record<Gap, string> = {
  "2xs": "gap-1", // 4px
  xs: "gap-2", //    8px
  sm: "gap-3", //    12px
  md: "gap-4", //    16px — default block gap
  lg: "gap-6", //    24px
  xl: "gap-8", //    32px
  "2xl": "gap-12", // 48px
};
