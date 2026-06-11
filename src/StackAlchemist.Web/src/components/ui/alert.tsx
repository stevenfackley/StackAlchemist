import type { ComponentProps, ReactNode } from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { AlertCircle, AlertTriangle, CheckCircle2, Info } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Inline status message. One place defines the error/success/info/warning shapes
 * that were previously copy-pasted as rose/emerald border+bg combos across pages.
 * The shape intentionally matches that legacy inline pattern so swaps are
 * visually neutral.
 */
const alertVariants = cva(
  "flex items-start gap-2.5 rounded-xl border px-4 py-3 font-mono text-xs",
  {
    variants: {
      variant: {
        error: "border-danger/30 bg-danger/10 text-danger",
        success: "border-success/30 bg-success/10 text-success",
        info: "border-accent/30 bg-accent/10 text-accent",
        warning: "border-yellow-400/30 bg-yellow-400/10 text-yellow-400",
      },
    },
    defaultVariants: { variant: "info" },
  },
);

const VARIANT_ICONS = {
  error: AlertCircle,
  success: CheckCircle2,
  info: Info,
  warning: AlertTriangle,
} as const;

export type AlertProps = ComponentProps<"div"> &
  VariantProps<typeof alertVariants> & {
    /** Pass false to render without an icon, or a node to replace the default. */
    icon?: ReactNode | false;
  };

export function Alert({ className, variant, icon, children, ...props }: AlertProps) {
  const DefaultIcon = VARIANT_ICONS[variant ?? "info"];
  // Errors interrupt (assertive); the rest inform (polite).
  const role = variant === "error" ? "alert" : "status";

  return (
    <div role={role} className={cn(alertVariants({ variant }), className)} {...props}>
      {icon === false ? null : (icon ?? <DefaultIcon aria-hidden className="mt-0.5 h-4 w-4 shrink-0" />)}
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}

export { alertVariants };
