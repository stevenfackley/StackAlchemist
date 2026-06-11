import type { ComponentProps } from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

/** Action buttons keep the brand pill shape; one place defines variants + sizing. */
const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 rounded-full font-mono uppercase tracking-[0.15em] transition-colors disabled:opacity-60 disabled:pointer-events-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent/60",
  {
    variants: {
      variant: {
        primary: "bg-accent text-white hover:bg-accent/90",
        success: "bg-success text-white hover:bg-success/90",
        ghost:
          "border border-border-strong text-ink-muted hover:border-accent/50 hover:text-accent",
        subtle: "text-ink-muted hover:text-accent",
        danger: "text-danger/70 hover:text-danger",
      },
      size: {
        sm: "px-4 py-2 text-[0.6875rem]",
        md: "px-4 py-2 text-xs",
        lg: "px-6 py-3 text-sm",
      },
    },
    defaultVariants: { variant: "primary", size: "md" },
  },
);

export type ButtonProps = ComponentProps<"button"> & VariantProps<typeof buttonVariants>;

export function Button({ className, variant, size, ...props }: ButtonProps) {
  return <button className={cn(buttonVariants({ variant, size }), className)} {...props} />;
}

export { buttonVariants };
